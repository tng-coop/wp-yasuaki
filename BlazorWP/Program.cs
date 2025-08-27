using Microsoft.AspNetCore.Components.Web;
using Microsoft.AspNetCore.Components.WebAssembly.Hosting;
using MudBlazor.Services;
using PanoramicData.Blazor.Extensions;
using System.Net.Http;

namespace BlazorWP
{
    public class Program
    {
        public static async Task Main(string[] args)
        {
            var builder = WebAssemblyHostBuilder.CreateDefault(args);

            builder.RootComponents.Add<App>("#app");
            builder.RootComponents.Add<HeadOutlet>("head::after");

            // shared state + coordination
            builder.Services.AddSingleton<IEndpointState, EndpointState>();
            builder.Services.AddSingleton<IAccessModeService, AccessModeService>();
            builder.Services.AddSingleton<IAccessGate, AccessGate>();

            // JS interop & auth primitives
            builder.Services.AddSingleton<LocalStorageJsInterop>();
            builder.Services.AddSingleton<WpNonceJsInterop>();
            builder.Services.AddSingleton<NonceService>();
            builder.Services.AddSingleton<INonceService>(sp => sp.GetRequiredService<NonceService>());
            builder.Services.AddSingleton<JwtService>();
            builder.Services.AddSingleton<IJwtService>(sp => sp.GetRequiredService<JwtService>());
            builder.Services.AddSingleton<AuthState>();

            // initializer + switcher
            builder.Services.AddSingleton<IAccessInitializer, AccessInitializer>();
            builder.Services.AddSingleton<IAccessSwitcher, AccessSwitcher>();

            // HTTP
            builder.Services.AddSingleton(sp =>
            {
                var handler = new AuthMessageHandler(sp.GetRequiredService<AuthState>())
                {
                    InnerHandler = new HttpClientHandler()
                };
                var http = new HttpClient(handler)
                {
                    BaseAddress = new Uri(builder.HostEnvironment.BaseAddress)
                };
                var ep = sp.GetRequiredService<IEndpointState>();
                ep.Changed += () => http.BaseAddress = ep.BaseAddress;
                return http;
            });

            builder.Services.AddSingleton<IWordPressClient, WordPressClient>();

            builder.Services.AddMudServices();
            builder.Services.AddPanoramicDataBlazor();
            builder.Services.AddAntDesign();
            builder.Services.AddSingleton<UploadPdfJsInterop>();
            builder.Services.AddSingleton<WpEndpointSyncJsInterop>();
            builder.Services.AddSingleton<SessionStorageJsInterop>();
            builder.Services.AddSingleton<WpMediaJsInterop>();

            var host = builder.Build();
            var config = host.Services.GetRequiredService<IConfiguration>();
            await host.RunAsync();
        }
    }
}
