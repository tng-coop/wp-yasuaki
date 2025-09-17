using WordPressPCL;
using System.Net.Http;
using System.Threading.Tasks;

namespace Editor.WordPress;

public interface IWordPressApiService
{
    void SetEndpoint(string endpoint);
    void SetAuthPreference(WordPressAuthPreference preference);
    WordPressAuthPreference AuthPreference { get; }
    Task<WordPressClient?> GetClientAsync();
    WordPressClient? Client { get; }
    HttpClient? HttpClient { get; }
}
