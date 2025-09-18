// tests/BlazorWP.Tests/Fakes.cs
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Editor.WordPress;
using WordPressPCL;

namespace BlazorWP.Tests;

internal sealed class FakeApiService : IWordPressApiService
{
    private string _endpoint = "https://example.com";
    private WordPressAuthPreference _authPreference = WordPressAuthPreference.None;

    public WordPressClient? Client { get; private set; }
    public HttpClient? HttpClient { get; private set; }
    public WordPressAuthPreference AuthPreference => _authPreference;

    // Required by IWordPressApiService
    public void SetEndpoint(string endpoint)
    {
        _endpoint = string.IsNullOrWhiteSpace(endpoint) ? "https://example.com" : endpoint;
        if (Client is not null) BuildClient();
    }

    // Required by IWordPressApiService
    public void SetAuthPreference(WordPressAuthPreference preference)
    {
        _authPreference = preference;
        // no-op in tests
    }

    public Task<WordPressClient?> GetClientAsync()
    {
        if (_returnNullClient) return Task.FromResult<WordPressClient?>(null);
        if (Client is null) BuildClient();
        return Task.FromResult(Client);
    }

private void BuildClient()
{
    var handler = new UsersMeFakeHandler();

    HttpClient?.Dispose();
    HttpClient = new HttpClient(handler)
    {
        // Match the real service: base is .../wp-json/
        BaseAddress = new Uri(_endpoint.TrimEnd('/') + "/wp-json/")
    };

    // ✅ this matches your production service usage
    Client = new WordPressClient(HttpClient);
}

    // test knob to simulate misconfiguration
    internal bool _returnNullClient;

    // Present on the interface; not used by our component assertions
    public Task<WpMe> GetCurrentUserAsync(CancellationToken ct = default)
        => throw new NotImplementedException();
}

internal sealed class UsersMeFakeHandler : HttpMessageHandler
{
    protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        if (request.RequestUri is { AbsolutePath: var path } &&
            path.Contains("/wp-json/wp/v2/users/me"))
        {
            var body = JsonSerializer.Serialize(new { id = 123, name = "Alice Test" });
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(body, Encoding.UTF8, "application/json")
            });
        }

        return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
    }
}
