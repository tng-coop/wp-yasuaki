using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Editor.Abstractions;

namespace Editor.WordPress;

public static class StreamingDI
{
    /// <summary>
    /// Register streaming services backed by IWordPressApiService.
    /// Host must also register IPostCache (e.g., MemoryPostCache).
    /// </summary>
    public static IServiceCollection AddWpdiStreaming(
        this IServiceCollection services,
        Func<StreamOptions>? configure = null)
    {
        var opts = configure?.Invoke() ?? new StreamOptions();
        services.AddSingleton<IOptions<StreamOptions>>(_ => Options.Create(opts));

        services.AddScoped<IContentStream>(sp =>
        {
            var api   = sp.GetRequiredService<IWordPressApiService>();
            var cache = sp.GetRequiredService<IPostCache>();
            return new ContentStream(api, cache);
        });

        services.AddSingleton<IPostFeed>(sp =>
        {
            var stream = sp.GetRequiredService<IContentStream>();
            var so     = sp.GetRequiredService<IOptions<StreamOptions>>();
            return new PostFeed(stream, so);
        });

        return services;
    }
}
