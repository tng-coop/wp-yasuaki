// WpDI/tests/Editor.Tests/Streaming/StreamingTestHost.cs
using Editor.Abstractions;
using Editor.WordPress;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

internal static class StreamingTestHost
{
    public static (ServiceProvider sp, WordPressApiService api) Build()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WP_BASE_URL")!;
        var user    = Environment.GetEnvironmentVariable("WP_USERNAME")!;
        var pass    = Environment.GetEnvironmentVariable("WP_APP_PASSWORD")!;

        var services = new ServiceCollection();

        // Configure WordPress options from env vars
        services.AddSingleton<IOptions<WordPressOptions>>(
            Options.Create(new WordPressOptions
            {
                BaseUrl    = baseUrl,
                UserName   = user,
                AppPassword= pass,
                Timeout    = TimeSpan.FromSeconds(20)
            })
        );

        // Register the API service (implements IWordPressApiService)
        services.AddSingleton<WordPressApiService>();
        services.AddSingleton<IWordPressApiService>(sp => sp.GetRequiredService<WordPressApiService>());

        // Cache + streaming
        services.AddSingleton<IPostCache, MemoryPostCache>();
        services.AddWpdiStreaming(); // <-- simplified, no httpProvider needed

        var sp = services.BuildServiceProvider();
        return (sp, sp.GetRequiredService<WordPressApiService>());
    }
}
