using System.Net.Http.Headers;
using System.Text;
using Editor.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Editor.WordPress;

public static class WordPressEditingDI
{
    public static IServiceCollection AddWordPressEditing(this IServiceCollection services, Action<WordPressOptions> configure)
    {
        services.Configure(configure);
        services.AddHttpClient<IPostEditor, WordPressEditor>((sp, http) =>
        {
            var o = sp.GetRequiredService<IOptions<WordPressOptions>>().Value;
            http.BaseAddress = new Uri(o.BaseUrl);
            http.Timeout = o.Timeout;
            var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{o.UserName}:{o.AppPassword}"));
            http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
            http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
            http.DefaultRequestHeaders.AcceptEncoding.ParseAdd("gzip");
        });
        return services;
    }
}
