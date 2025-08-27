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

            // state + coordination
            builder.Services.AddScoped<IEndpointState, EndpointState>();
            builder.Services.AddScoped<IAccessModeService, AccessModeService>();
            builder.Services.AddScoped<IAccessGate, AccessGate>();

            // auth primitives
            builder.Services.AddScoped<WpNonceJsInterop>();
            builder.Services.AddScoped<NonceService>();
            builder.Services.AddScoped<INonceService>(sp => sp.GetRequiredService<NonceService>());
            builder.Services.AddScoped<JwtService>();
            builder.Services.AddScoped<IJwtService>(sp => sp.GetRequiredService<JwtService>());

            // initializer + switcher
            builder.Services.AddScoped<IAccessInitializer, AccessInitializer>();
            builder.Services.AddScoped<IAccessSwitcher, AccessSwitcher>();

            // HTTP
            builder.Services.AddScoped<AuthMessageHandler>();
            builder.Services.AddHttpClient("WpApi")
                .AddHttpMessageHandler<AuthMessageHandler>();
            builder.Services.AddScoped(sp => {
                var factory = sp.GetRequiredService<IHttpClientFactory>();
                var ep = sp.GetRequiredService<IEndpointState>();
                var http = factory.CreateClient("WpApi");
                http.BaseAddress = ep.BaseAddress;
                return http;
            });

            builder.Services.AddMudServices();
            builder.Services.AddPanoramicDataBlazor();
            builder.Services.AddAntDesign();
            builder.Services.AddScoped<UploadPdfJsInterop>();
            builder.Services.AddScoped<WpEndpointSyncJsInterop>();
            builder.Services.AddScoped<LocalStorageJsInterop>();
            builder.Services.AddScoped<SessionStorageJsInterop>();
            builder.Services.AddScoped<WpMediaJsInterop>();

            var host = builder.Build();
            var config = host.Services.GetRequiredService<IConfiguration>();
            await host.RunAsync();
        }
    }
}
