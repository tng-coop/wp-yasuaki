using BlazorWP.Data;
using Editor.WordPress;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Primitives;
using TG.Blazor.IndexedDB;

// ADD: WPDI abstractions (IPostEditor, IPostFeed, IPostCache, StreamOptions)
using Editor.Abstractions;

namespace BlazorWP
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // 1) This pulls in wwwroot/appsettings.json (+ env overrides)
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            // 2) Root components
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            // 3) Services
            builder.Services.AddScoped(_ => new HttpClient { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) });
            builder.Services.Configure<WordPressOptions>(options =>
            {
                options.BaseUrl = string.Empty;
                options.UserName = string.Empty;
                options.AppPassword = string.Empty;
                options.Timeout = TimeSpan.FromSeconds(30);
            });

            builder.Services.AddScoped<AppPasswordService>();
            builder.Services.AddScoped<UploadPdfJsInterop>();
            builder.Services.AddScoped<WpNonceJsInterop>();
            builder.Services.AddSingleton<LocalStorageJsInterop>();
            builder.Services.AddScoped<SessionStorageJsInterop>();
            builder.Services.AddScoped<CredentialManagerJsInterop>();
            builder.Services.AddScoped<ClipboardJsInterop>();
            builder.Services.AddScoped<WpMediaJsInterop>();

            builder.Services.AddSingleton<IWordPressApiService, WordPressApiService>();
            builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
            builder.Services.AddSingleton<LanguageService>();
            builder.Services.AddSingleton<AppFlags>();

            builder.Services.AddIndexedDB(db =>
            {
                db.DbName = "BlazorWPDB";
                db.Version = 2;

                db.Stores.Add(new StoreSchema
                {
                    Name = "notes",
                    PrimaryKey = new IndexSpec { Name = "id", KeyPath = "id", Auto = false }
                });

                db.Stores.Add(new StoreSchema
                {
                    Name = "kv",
                    PrimaryKey = new IndexSpec { Name = "id", KeyPath = "id", Auto = false }
                });
            });

            builder.Services.AddScoped<ILocalStore, IndexedDbLocalStore>();

            // WPDI caching + services
            builder.Services.AddSingleton<IPostCache, MemoryPostCache>();

            builder.Services.AddWordPressEditing();

            // 5) Build the host
            var host = builder.Build();

            // 6) Flags + storage (config not required anymore)
            var flags = host.Services.GetRequiredService<AppFlags>();
            var storage = host.Services.GetRequiredService<LocalStorageJsInterop>();
            var languageService = host.Services.GetRequiredService<LanguageService>();
            var navigationManager = host.Services.GetRequiredService<NavigationManager>();
            var api = host.Services.GetRequiredService<IWordPressApiService>();
            var appPasswordService = host.Services.GetRequiredService<AppPasswordService>();

            var uri = new Uri(navigationManager.Uri);
            var queryParams = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);

            // ---------- App Mode ----------
            var appMode = AppMode.Full;
            if (queryParams.TryGetValue("appmode", out var modeValues))
            {
                var val = modeValues.ToString();
                if (val.Equals("basic", StringComparison.OrdinalIgnoreCase))
                    appMode = AppMode.Basic;
            }
            else
            {
                var storedMode = await storage.GetItemAsync("appmode");
                if (storedMode?.Equals("basic", StringComparison.OrdinalIgnoreCase) == true)
                    appMode = AppMode.Basic;
            }
            await flags.SetAppMode(appMode);

            // ---------- Auth Mode ----------
            var authMode = AuthType.AppPass;
            if (queryParams.TryGetValue("auth", out var authValues))
            {
                if (authValues.ToString().Equals("nonce", StringComparison.OrdinalIgnoreCase))
                    authMode = AuthType.Nonce;
            }
            else
            {
                var storedAuth = await storage.GetItemAsync("auth");
                if (storedAuth?.Equals("nonce", StringComparison.OrdinalIgnoreCase) == true)
                    authMode = AuthType.Nonce;
            }
            await flags.SetAuthMode(authMode);

            // ---------- Language ----------
            var lang = "en";
            if (queryParams.TryGetValue("lang", out var langValues))
            {
                if (langValues.ToString().Equals("jp", StringComparison.OrdinalIgnoreCase))
                    lang = "jp";
            }
            else
            {
                var storedLang = await storage.GetItemAsync("lang");
                if (storedLang?.Equals("jp", StringComparison.OrdinalIgnoreCase) == true)
                    lang = "jp";
            }
            var culture = lang == "jp" ? "ja-JP" : "en-US";
            languageService.SetCulture(culture);
            await flags.SetLanguage(lang == "jp" ? Language.Japanese : Language.English);

            // ---------- WordPress URL ----------
            // Precedence: Query (?wpurl=) → LocalStorage ("wpEndpoint") → Launch origin (lowest)
            var launchOrigin = $"{uri.Scheme}://{uri.Authority}";
            var wpurl = launchOrigin;

            // 1) Query override
            if (queryParams.TryGetValue("wpurl", out var wpurlValues))
            {
                var val = (wpurlValues.ToString() ?? "").Trim();
                if (!string.IsNullOrEmpty(val))
                    wpurl = val;
            }
            // 2) LocalStorage override
            else
            {
                var storedWp = (await storage.GetItemAsync("wpEndpoint"))?.Trim();
                if (!string.IsNullOrEmpty(storedWp))
                    wpurl = storedWp!;
                // 3) Otherwise keep launchOrigin (lowest)
            }

            // Basic sanity (absolute http/https)
            if (!Uri.TryCreate(wpurl, UriKind.Absolute, out var wpUri) ||
                (wpUri.Scheme != Uri.UriSchemeHttp && wpUri.Scheme != Uri.UriSchemeHttps))
            {
                throw new InvalidOperationException(
                    $"❌ Invalid WordPress endpoint '{wpurl}'. Must be an absolute http(s) URL.");
            }

            await flags.SetWpUrl(wpurl);
            api.SetEndpoint(wpurl);

            WordPressAuthPreference authPreference;
            if (authMode == AuthType.Nonce)
            {
                var nonceJs = host.Services.GetRequiredService<WpNonceJsInterop>();
                authPreference = WordPressAuthPreference.Nonce(() => nonceJs.GetNonceAsync());
            }
            else
            {
                var creds = await appPasswordService.GetAsync();
                authPreference = creds is { Username: var u, AppPassword: var p }
                    ? WordPressAuthPreference.AppPassword(u, p)
                    : WordPressAuthPreference.None;
            }
            api.SetAuthPreference(authPreference);

            // ---------- Normalize URL query (write current flags + wpurl) ----------
            var needsNormalization =
                !queryParams.TryGetValue("lang", out var existingLang) ||
                !existingLang.ToString().Equals(lang, StringComparison.OrdinalIgnoreCase) ||
                !queryParams.TryGetValue("appmode", out var existingMode) ||
                !existingMode.ToString().Equals(appMode == AppMode.Basic ? "basic" : "full", StringComparison.OrdinalIgnoreCase) ||
                !queryParams.TryGetValue("auth", out var existingAuth) ||
                !existingAuth.ToString().Equals(authMode == AuthType.Nonce ? "nonce" : "apppass", StringComparison.OrdinalIgnoreCase) ||
                !queryParams.TryGetValue("wpurl", out var existingWpUrl) ||
                !existingWpUrl.ToString().Equals(wpurl, StringComparison.Ordinal);

            if (needsNormalization)
            {
                var segments = new List<string>();
                foreach (var kvp in queryParams)
                {
                    if (kvp.Key.Equals("lang", StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Equals("appmode", StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Equals("auth", StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Equals("wpurl", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    if (StringValues.IsNullOrEmpty(kvp.Value))
                    {
                        segments.Add(Uri.EscapeDataString(kvp.Key));
                    }
                    else
                    {
                        foreach (var v in kvp.Value)
                        {
                            segments.Add($"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(v ?? string.Empty)}");
                        }
                    }
                }

                segments.Add($"appmode={(appMode == AppMode.Basic ? "basic" : "full")}");
                segments.Add($"lang={lang}");
                segments.Add($"auth={(authMode == AuthType.Nonce ? "nonce" : "apppass")}");
                segments.Add($"wpurl={Uri.EscapeDataString(wpurl)}");

                var newQuery = string.Join("&", segments);
                var normalizedUri = uri.GetLeftPart(UriPartial.Path) + (newQuery.Length > 0 ? "?" + newQuery : string.Empty);
                navigationManager.NavigateTo(normalizedUri, replace: true);
            }

            // 7) Run app
            await host.RunAsync();
        }
    }
}
