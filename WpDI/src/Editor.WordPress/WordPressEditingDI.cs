using Editor.Abstractions;
using Microsoft.Extensions.DependencyInjection;

namespace Editor.WordPress
{
    public static class WordPressEditingDI
    {
        /// <summary>
        /// Register IPostEditor backed by WordPressEditor, which uses IWordPressApiService.
        /// Host must register IWordPressApiService separately.
        /// </summary>
        public static IServiceCollection AddWordPressEditing(this IServiceCollection services)
        {
            if (services == null) throw new ArgumentNullException(nameof(services));

            services.AddScoped<IPostEditor>(sp =>
            {
                var api = sp.GetRequiredService<IWordPressApiService>();
                return new WordPressEditor(api);
            });

            return services;
        }
    }
}
