using WordPressPCL;
using System.Net.Http;
using System;
using System.Threading.Tasks;
using Editor.WordPress;

namespace BlazorWP;

public sealed class WordPressApiService : IWordPressApiService
{
    private readonly AuthMessageHandler _auth;
    private readonly AppFlags _flags;
    private WordPressClient? _client;
    private HttpClient? _httpClient;

    public WordPressApiService(AuthMessageHandler auth, AppFlags flags)
    {
        _auth = auth;
        _flags = flags;
    }

    public void SetEndpoint(string endpoint)
    {
        var baseUrl = endpoint.TrimEnd('/') + "/wp-json/";
        _httpClient = new HttpClient(_auth) { BaseAddress = new Uri(baseUrl) };
        _client = new WordPressClient(_httpClient);
    }

    public async Task<WordPressClient?> GetClientAsync()
    {
        if (_client != null)
        {
            return _client;
        }
        var endpoint = _flags.WpUrl;
        if (string.IsNullOrEmpty(endpoint))
        {
            return null;
        }
        SetEndpoint(endpoint);
        return _client;
    }

    public WordPressClient? Client => _client;
    public HttpClient? HttpClient => _httpClient;
}
