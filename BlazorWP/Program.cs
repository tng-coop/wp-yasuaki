using BlazorWP.Data;
using Editor.WordPress;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.Extensions.Primitives;
using TG.Blazor.IndexedDB;
using Microsoft.AspNetCore.WebUtilities;

namespace BlazorWP
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            // 1) This pulls in wwwroot/appsettings.json (+ env overrides)
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            // 2) Register your root components
            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            // 3) Your services
            builder.Services.AddScoped<AuthMessageHandler>();
            builder.Services.AddScoped(sp =>
            {
                var handler = sp.GetRequiredService<AuthMessageHandler>();
                return new HttpClient(handler) { BaseAddress = new Uri(builder.HostEnvironment.BaseAddress) };
            });
            builder.Services.AddScoped<AppPasswordService>();
            builder.Services.AddScoped<UploadPdfJsInterop>();
            builder.Services.AddScoped<WpNonceJsInterop>();
            builder.Services.AddSingleton<LocalStorageJsInterop>();
            builder.Services.AddScoped<SessionStorageJsInterop>();
            builder.Services.AddScoped<CredentialManagerJsInterop>();
            builder.Services.AddScoped<ClipboardJsInterop>();
            builder.Services.AddScoped<WpMediaJsInterop>();
            builder.Services.AddScoped<IWordPressApiService, WordPressApiService>();
            builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
            builder.Services.AddSingleton<LanguageService>();
            builder.Services.AddSingleton<AppFlags>();

            builder.Services.AddIndexedDB(db =>
            {
                // inside builder.Services.AddIndexedDB(db => { ... });
                db.DbName = "BlazorWPDB";
                db.Version = 2; // ⬅️ bump this when you add the new store

                db.Stores.Add(new StoreSchema
                {
                    Name = "notes",
                    PrimaryKey = new IndexSpec { Name = "id", KeyPath = "id", Auto = false }
                });

                // NEW: generic KV store with string key `id`
                db.Stores.Add(new StoreSchema
                {
                    Name = "kv",
                    PrimaryKey = new IndexSpec { Name = "id", KeyPath = "id", Auto = false }
                });

            });

            builder.Services.AddScoped<ILocalStore, IndexedDbLocalStore>();

            // 5) Build the host (this hooks up the logging provider)
            var host = builder.Build();

            // 6) Now that the JSON has been loaded, enumerate via ILogger
            var config = host.Services.GetRequiredService<IConfiguration>();
            var flags = host.Services.GetRequiredService<AppFlags>();
            var storage = host.Services.GetRequiredService<LocalStorageJsInterop>();

            // Set culture from query parameter before first render
            var languageService = host.Services.GetRequiredService<LanguageService>();
            var navigationManager = host.Services.GetRequiredService<NavigationManager>();
            var uri = new Uri(navigationManager.Uri);
            var queryParams = QueryHelpers.ParseQuery(uri.Query);

            // Determine app mode
            var appMode = AppMode.Full;
            if (queryParams.TryGetValue("appmode", out var modeValues))
            {
                var val = modeValues.ToString();
                if (val.Equals("basic", StringComparison.OrdinalIgnoreCase))
                {
                    appMode = AppMode.Basic;
                }
            }
            else
            {
                var storedMode = await storage.GetItemAsync("appmode");
                if (storedMode?.Equals("basic", StringComparison.OrdinalIgnoreCase) == true)
                {
                    appMode = AppMode.Basic;
                }
            }

            await flags.SetAppMode(appMode);

            var authMode = AuthType.AppPass;
            if (queryParams.TryGetValue("auth", out var authValues))
            {
                if (authValues.ToString().Equals("nonce", StringComparison.OrdinalIgnoreCase))
                {
                    authMode = AuthType.Nonce;
                }
            }
            else
            {
                var storedAuth = await storage.GetItemAsync("auth");
                if (storedAuth?.Equals("nonce", StringComparison.OrdinalIgnoreCase) == true)
                {
                    authMode = AuthType.Nonce;
                }
            }

            await flags.SetAuthMode(authMode);

            var lang = "en";
            if (queryParams.TryGetValue("lang", out var langValues))
            {
                if (langValues.ToString().Equals("jp", StringComparison.OrdinalIgnoreCase))
                {
                    lang = "jp";
                }
            }
            else
            {
                var storedLang = await storage.GetItemAsync("lang");
                if (storedLang?.Equals("jp", StringComparison.OrdinalIgnoreCase) == true)
                {
                    lang = "jp";
                }
            }

            var culture = lang == "jp" ? "ja-JP" : "en-US";
            languageService.SetCulture(culture);
            await flags.SetLanguage(lang == "jp" ? Language.Japanese : Language.English);

            var wpurl = config["WordPress:Url"] ?? string.Empty;
            if (queryParams.TryGetValue("wpurl", out var wpurlValues))
            {
                var val = wpurlValues.ToString();
                if (!string.IsNullOrEmpty(val))
                {
                    wpurl = val;
                }
            }
            else
            {
                var storedWp = await storage.GetItemAsync("wpEndpoint");
                if (!string.IsNullOrEmpty(storedWp))
                {
                    wpurl = storedWp;
                }
            }

            await flags.SetWpUrl(wpurl);

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

            // 7) And finally run
            await host.RunAsync();
        }
    }
}
