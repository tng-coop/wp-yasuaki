using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Editor.Abstractions;

namespace Editor.WordPress;

public static class StreamingDI
{
    public static IServiceCollection AddWpdiStreaming(
        this IServiceCollection services,
        Func<IServiceProvider, HttpClient> httpProvider,
        Func<StreamOptions>? configure = null)
    {
        var opts = configure?.Invoke() ?? new StreamOptions();
        services.AddSingleton<IOptions<StreamOptions>>(_ => Options.Create(opts));

        services.AddScoped<IContentStream>(sp =>
        {
            var http = httpProvider(sp) ?? throw new InvalidOperationException("HttpClient is null");
            var cache = sp.GetRequiredService<IPostCache>();
            return new ContentStream(http, cache);
        });

        services.AddSingleton<IPostFeed>(sp =>
        {
            var stream = sp.GetRequiredService<IContentStream>();
            var so = sp.GetRequiredService<IOptions<StreamOptions>>();
            return new PostFeed(stream, so);
        });

        return services;
    }
}
