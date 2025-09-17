using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.Extensions.Options;
using WordPressPCL;

namespace Editor.WordPress;

public sealed class WordPressApiService : IWordPressApiService
{
    private readonly object _sync = new();
    private readonly TimeSpan _timeout;
    private readonly Func<HttpMessageHandler>? _primaryHandlerFactory;

    private Uri? _baseUri;
    private HttpClient? _httpClient;
    private WordPressClient? _client;
    private WordPressAuthPreference _authPreference;

    public WordPressApiService(IOptions<WordPressOptions> options)
        : this(options, null)
    {
    }

    public WordPressApiService(IOptions<WordPressOptions> options, Func<HttpMessageHandler>? primaryHandlerFactory)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        var opt = options.Value ?? throw new ArgumentException("Options value cannot be null.", nameof(options));

        _primaryHandlerFactory = primaryHandlerFactory;
        _timeout = opt.Timeout > TimeSpan.Zero ? opt.Timeout : TimeSpan.FromSeconds(10);
        _authPreference = !string.IsNullOrWhiteSpace(opt.UserName) && !string.IsNullOrWhiteSpace(opt.AppPassword)
            ? WordPressAuthPreference.AppPassword(opt.UserName, opt.AppPassword)
            : WordPressAuthPreference.None;

        if (!string.IsNullOrWhiteSpace(opt.BaseUrl))
        {
            SetEndpoint(opt.BaseUrl);
        }
    }

    public WordPressAuthPreference AuthPreference => Volatile.Read(ref _authPreference);

    public void SetAuthPreference(WordPressAuthPreference preference)
    {
        if (preference is null) throw new ArgumentNullException(nameof(preference));
        Volatile.Write(ref _authPreference, preference);
    }

    public void SetEndpoint(string endpoint)
    {
        if (endpoint == null) throw new ArgumentNullException(nameof(endpoint));
        var trimmed = endpoint.Trim();
        if (trimmed.Length == 0)
            throw new ArgumentException("Endpoint must not be empty.", nameof(endpoint));

        var baseUrl = trimmed.TrimEnd('/') + "/wp-json/";
        var baseUri = new Uri(baseUrl, UriKind.Absolute);

        lock (_sync)
        {
            _baseUri = baseUri;
            if (_httpClient is null)
            {
                _httpClient = CreateHttpClient();
            }

            _httpClient.BaseAddress = baseUri;
            _client = new WordPressClient(_httpClient);
        }
    }

    public Task<WordPressClient?> GetClientAsync()
    {
        lock (_sync)
        {
            if (_client is not null)
            {
                return Task.FromResult<WordPressClient?>(_client);
            }

            if (_baseUri is null)
            {
                return Task.FromResult<WordPressClient?>(null);
            }

            if (_httpClient is null)
            {
                _httpClient = CreateHttpClient();
                _httpClient.BaseAddress = _baseUri;
            }

            _client = new WordPressClient(_httpClient);
            return Task.FromResult<WordPressClient?>(_client);
        }
    }

    public WordPressClient? Client => Volatile.Read(ref _client);

    public HttpClient? HttpClient => Volatile.Read(ref _httpClient);

    private HttpClient CreateHttpClient()
    {
        var handler = new AuthDispatchingHandler(GetAuthPreference)
        {
            InnerHandler = _primaryHandlerFactory?.Invoke() ?? new HttpClientHandler()
        };

        var http = new HttpClient(handler)
        {
            Timeout = _timeout
        };
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");

        if (_baseUri is not null)
        {
            http.BaseAddress = _baseUri;
        }

        return http;
    }

    private WordPressAuthPreference GetAuthPreference() => Volatile.Read(ref _authPreference);

    private sealed class AuthDispatchingHandler : DelegatingHandler
    {
        private readonly Func<WordPressAuthPreference> _getPreference;

        public AuthDispatchingHandler(Func<WordPressAuthPreference> getPreference)
        {
            _getPreference = getPreference ?? throw new ArgumentNullException(nameof(getPreference));
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var preference = _getPreference();

            switch (preference.Mode)
            {
                case WordPressAuthMode.AppPassword:
                    request.SetBrowserRequestCredentials(BrowserRequestCredentials.Omit);
                    request.Headers.Remove("X-WP-Nonce");
                    request.Headers.Remove("Authorization");
                    if (!BasicAuth.ShouldSkip(request) && preference.BasicCredentials is { } creds)
                    {
                        BasicAuth.Apply(request, creds.UserName, creds.AppPassword);
                    }
                    break;

                case WordPressAuthMode.Nonce:
                    request.Headers.Remove("Authorization");
                    request.SetBrowserRequestCredentials(BrowserRequestCredentials.Include);
                    if (!BasicAuth.ShouldSkip(request) && preference.NonceFactory is { } nonceFactory)
                    {
                        var nonce = await nonceFactory(cancellationToken).ConfigureAwait(false);
                        if (!string.IsNullOrWhiteSpace(nonce))
                        {
                            request.Headers.Remove("X-WP-Nonce");
                            request.Headers.Add("X-WP-Nonce", nonce);
                        }
                        else
                        {
                            request.Headers.Remove("X-WP-Nonce");
                        }
                    }
                    else
                    {
                        request.Headers.Remove("X-WP-Nonce");
                    }
                    break;

                default:
                    request.Headers.Remove("Authorization");
                    request.Headers.Remove("X-WP-Nonce");
                    request.SetBrowserRequestCredentials(BrowserRequestCredentials.Omit);
                    break;
            }

            return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
        }
    }
}
