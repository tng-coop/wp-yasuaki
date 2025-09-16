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
            builder.Services.AddWordPressEditingFromHttp(sp =>
                sp.GetRequiredService<IWordPressApiService>().HttpClient!);

            // CHANGED: no HttpClient factory parameter; ContentStream will resolve IWordPressApiService.
            builder.Services.AddWpdiStreaming(
                configure: () => new StreamOptions(WarmFirstCount: 10, MaxBatchSize: 100)
            );

            // 5) Build the host
            var host = builder.Build();

            // 6) Configuration + flags + storage
            var config = host.Services.GetRequiredService<IConfiguration>();
            var flags = host.Services.GetRequiredService<AppFlags>();
            var storage = host.Services.GetRequiredService<LocalStorageJsInterop>();
            var languageService = host.Services.GetRequiredService<LanguageService>();
            var navigationManager = host.Services.GetRequiredService<NavigationManager>();

            var uri = new Uri(navigationManager.Uri);
            var queryParams = Microsoft.AspNetCore.WebUtilities.QueryHelpers.ParseQuery(uri.Query);

            // (rest of your Program.cs remains unchanged)
            // ...
            await host.RunAsync();
        }
    }
}
