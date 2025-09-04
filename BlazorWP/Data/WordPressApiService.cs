using WordPressPCL;
using System.Net.Http;
using System;
using System.Threading.Tasks;

namespace BlazorWP;

public sealed class WordPressApiService : IWordPressApiService
{
    private readonly AuthMessageHandler _auth;
    private readonly LocalStorageJsInterop _storage;
    private WordPressClient? _client;
    private HttpClient? _httpClient;

    public WordPressApiService(AuthMessageHandler auth, LocalStorageJsInterop storage)
    {
        _auth = auth;
        _storage = storage;
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
        var endpoint = await _storage.GetItemAsync("wpEndpoint");
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
