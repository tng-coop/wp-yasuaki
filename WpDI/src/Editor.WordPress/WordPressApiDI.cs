using System;
using Microsoft.Extensions.DependencyInjection;

namespace Editor.WordPress;

public static class WordPressApiDI
{
    public static IServiceCollection AddWordPressApiAppPassword(this IServiceCollection services, Action<WordPressOptions> configure)
    {
        if (services == null) throw new ArgumentNullException(nameof(services));
        if (configure == null) throw new ArgumentNullException(nameof(configure));

        services.Configure(configure);
        services.AddSingleton<IWordPressApiService, WordPressApiService>();
        return services;
    }
}
