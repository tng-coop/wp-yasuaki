using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Editor.WordPress;

public static class EditLockDI
{
    public static IServiceCollection AddWpdiEditLocks(
        this IServiceCollection services,
        Func<IServiceProvider, HttpClient> httpProvider)
    {
        services.AddOptions<EditLockOptions>();
        services.AddScoped<IEditLockService>(sp =>
        {
            var http = httpProvider(sp);
            var opts = sp.GetRequiredService<IOptions<EditLockOptions>>().Value;
            return new EditLockService(http, opts);
        });
        return services;
    }
}
