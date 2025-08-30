using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using PanoramicData.Blazor.Extensions;
using System.Globalization;

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
            builder.Services.AddLocalization();
            builder.Services.AddScoped<LanguageService>();

            // 5) Build the host (this hooks up the logging provider)
            var host = builder.Build();

            // 6) Now that the JSON has been loaded, enumerate via ILogger
            var config = host.Services.GetRequiredService<IConfiguration>();

            // Set default culture
            var languageService = host.Services.GetRequiredService<LanguageService>();
            languageService.SetCulture("en-US");

            // 7) And finally run
            await host.RunAsync();
        }
    }
}
