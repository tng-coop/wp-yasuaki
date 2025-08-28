using WordPressPCL;
using System.Net.Http;
using System;
using System.Threading.Tasks;

namespace BlazorWP;

public sealed class WordPressApiService
{
    private readonly AuthMessageHandler _auth;
    private readonly LocalStorageJsInterop _storage;
    private WordPressClient? _client;

    public WordPressApiService(AuthMessageHandler auth, LocalStorageJsInterop storage)
    {
        _auth = auth;
        _storage = storage;
    }

    public void SetEndpoint(string endpoint)
    {
        var baseUrl = endpoint.TrimEnd('/') + "/wp-json/";
        _client = new WordPressClient(new HttpClient(_auth) { BaseAddress = new Uri(baseUrl) });
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
}
