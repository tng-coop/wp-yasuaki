using BlazorWP.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Primitives;
using PanoramicData.Blazor.Extensions;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

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
            builder.Services.AddPanoramicDataBlazor();
            builder.Services.AddScoped<JwtService>();
            builder.Services.AddScoped<UploadPdfJsInterop>();
            builder.Services.AddScoped<WpNonceJsInterop>();
            builder.Services.AddScoped<WpEndpointSyncJsInterop>();
            builder.Services.AddScoped<LocalStorageJsInterop>();
            builder.Services.AddScoped<SessionStorageJsInterop>();
            builder.Services.AddScoped<CredentialManagerJsInterop>();
            builder.Services.AddScoped<ClipboardJsInterop>();
            builder.Services.AddScoped<WpMediaJsInterop>();
            builder.Services.AddScoped<WordPressApiService>();
            builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
            builder.Services.AddSingleton<LanguageService>();
            builder.Services.AddSingleton<AppFlags>();

            // 5) Build the host (this hooks up the logging provider)
            var host = builder.Build();

            // 6) Now that the JSON has been loaded, enumerate via ILogger
            var config = host.Services.GetRequiredService<IConfiguration>();
            var flags = host.Services.GetRequiredService<AppFlags>();
            // Set culture from query parameter before first render
            var languageService = host.Services.GetRequiredService<LanguageService>();
            var navigationManager = host.Services.GetRequiredService<NavigationManager>();
            var uri = new Uri(navigationManager.Uri);
            var queryParams = QueryHelpers.ParseQuery(uri.Query);

            // Determine basic/full mode
            var basic = false;
            if (queryParams.TryGetValue("basic", out var basicValues))
            {
                var val = basicValues.ToString();
                if (string.IsNullOrEmpty(val) ||
                    val.Equals("true", StringComparison.OrdinalIgnoreCase) ||
                    val.Equals("1"))
                {
                    basic = true;
                }
            }
            else if (queryParams.TryGetValue("full", out var _))
            {
                basic = false;
            }

            flags.SetBasic(basic);

            var lang = "en";
            if (queryParams.TryGetValue("lang", out var langValues) &&
                langValues.ToString().Equals("ja", StringComparison.OrdinalIgnoreCase))
            {
                lang = "ja";
            }
            else if (queryParams.ContainsKey("ja"))
            {
                lang = "ja";
            }

            var culture = lang == "ja" ? "ja-JP" : "en-US";
            languageService.SetCulture(culture);

            var needsNormalization =
                !queryParams.TryGetValue("lang", out var existingLang) ||
                !existingLang.ToString().Equals(lang, StringComparison.OrdinalIgnoreCase) ||
                queryParams.ContainsKey("ja") ||
                !queryParams.TryGetValue("basic", out var existingBasic) ||
                !existingBasic.ToString().Equals(basic.ToString().ToLowerInvariant(), StringComparison.OrdinalIgnoreCase) ||
                queryParams.ContainsKey("full");

            if (needsNormalization)
            {
                var segments = new List<string>();
                foreach (var kvp in queryParams)
                {
                    if (kvp.Key.Equals("lang", StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Equals("ja", StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Equals("basic", StringComparison.OrdinalIgnoreCase) ||
                        kvp.Key.Equals("full", StringComparison.OrdinalIgnoreCase))
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
                            segments.Add($"{Uri.EscapeDataString(kvp.Key)}={Uri.EscapeDataString(v)}");
                        }
                    }
                }

                segments.Add($"basic={basic.ToString().ToLowerInvariant()}");
                segments.Add($"lang={lang}");

                var newQuery = string.Join("&", segments);
                var normalizedUri = uri.GetLeftPart(UriPartial.Path) + (newQuery.Length > 0 ? "?" + newQuery : string.Empty);
                navigationManager.NavigateTo(normalizedUri, replace: true);
            }

            // 7) And finally run
            await host.RunAsync();
        }
    }
}
