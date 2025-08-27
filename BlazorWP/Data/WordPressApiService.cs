using WordPressPCL;

namespace BlazorWP;

public sealed class WordPressApiService
{
    private readonly AuthMessageHandler _auth;
    private readonly LocalStorageJsInterop _storage;
    private WordPressClient? _client;
    private HttpClient? _httpClient;
    private string? _endpoint;
    private string? _baseUrl;

    public WordPressApiService(AuthMessageHandler auth, LocalStorageJsInterop storage)
    {
        _auth = auth;
        _storage = storage;
    }

    public async Task<WordPressClient?> GetClientAsync()
    {
        var endpoint = await _storage.GetItemAsync("wpEndpoint");
        if (string.IsNullOrEmpty(endpoint))
        {
            _client = null;
            _httpClient = null;
            _baseUrl = null;
            _endpoint = null;
            return null;
        }

        if (_client == null || !string.Equals(endpoint, _endpoint, StringComparison.OrdinalIgnoreCase))
        {
            SetEndpoint(endpoint);
        }

        return _client;
    }

    public async Task<HttpClient?> GetHttpClientAsync()
    {
        await GetClientAsync();
        return _httpClient;
    }

    public void SetEndpoint(string endpoint)
    {
        _endpoint = endpoint;
        _baseUrl = endpoint.TrimEnd('/') + "/wp-json/";
        _httpClient = new HttpClient(_auth) { BaseAddress = new Uri(_baseUrl) };
        _client = new WordPressClient(_httpClient);
    }

    public string? BaseUrl => _baseUrl;
}
