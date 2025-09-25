using System.Threading;
using System.Threading.Tasks;
using WordPressPCL;

namespace Editor.WordPress
{
    public interface IWordPressApiService
    {
        void SetEndpoint(string endpoint);
        void SetAuthPreference(WordPressAuthPreference preference);
        WordPressAuthPreference AuthPreference { get; }

        Task<WordPressClient?> GetClientAsync();
        WordPressClient? Client { get; }
        HttpClient? HttpClient { get; }

        /// <summary>
        /// Calls wp/v2/users/me and returns the current user.
        /// On failure, throws a WpdiException (AuthError, RateLimited, TimeoutError, etc.)
        /// produced by our Http policy/handlers; non-auth 4xx are mapped here.
        /// </summary>
        Task<WpMe> GetCurrentUserAsync(CancellationToken ct = default);
        Task<T?> PostJsonAsync<T>(string path, object body, CancellationToken ct = default);
    }
}
