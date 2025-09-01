using BlazorWP.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PanoramicData.Blazor.Extensions;
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
            builder.Services.AddScoped<INonceService, NonceService>();
            builder.Services.AddScoped<WpEndpointSyncJsInterop>();
            builder.Services.AddScoped<LocalStorageJsInterop>();
            builder.Services.AddScoped<SessionStorageJsInterop>();
            builder.Services.AddScoped<CredentialManagerJsInterop>();
            builder.Services.AddScoped<ClipboardJsInterop>();
            builder.Services.AddScoped<WpMediaJsInterop>();
            builder.Services.AddScoped<WordPressApiService>();
            builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");
            builder.Services.AddSingleton<LanguageService>();

            // 5) Build the host (this hooks up the logging provider)
            var host = builder.Build();

            // 6) Now that the JSON has been loaded, enumerate via ILogger
            var config = host.Services.GetRequiredService<IConfiguration>();

            // Set culture from query parameter, normalizing to ?lang=en or ?lang=ja
            var languageService = host.Services.GetRequiredService<LanguageService>();
            var navigationManager = host.Services.GetRequiredService<NavigationManager>();
            var uri = new Uri(navigationManager.Uri);
            var query = uri.Query.TrimStart('?');
            var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

            string lang = "en";
            foreach (var part in parts)
            {
                var kv = part.Split('=', 2);
                if (kv.Length == 2 && kv[0].Equals("lang", StringComparison.OrdinalIgnoreCase))
                {
                    if (kv[1].Equals("ja", StringComparison.OrdinalIgnoreCase))
                    {
                        lang = "ja";
                    }
                    break;
                }

                if (kv.Length == 1)
                {
                    if (kv[0].Equals("ja", StringComparison.OrdinalIgnoreCase) || kv[0].Equals("jp", StringComparison.OrdinalIgnoreCase))
                    {
                        lang = "ja";
                        break;
                    }
                    if (kv[0].Equals("en", StringComparison.OrdinalIgnoreCase))
                    {
                        lang = "en";
                        break;
                    }
                }
            }

            var expectedQuery = $"?lang={lang}";
            if (!string.Equals(uri.Query, expectedQuery, StringComparison.OrdinalIgnoreCase))
            {
                var newUri = uri.GetLeftPart(UriPartial.Path) + expectedQuery + uri.Fragment;
                navigationManager.NavigateTo(newUri, replace: true);
            }

            var culture = lang == "ja" ? "ja-JP" : "en-US";
            languageService.SetCulture(culture);

            // 7) And finally run
            await host.RunAsync();
        }
    }
}
