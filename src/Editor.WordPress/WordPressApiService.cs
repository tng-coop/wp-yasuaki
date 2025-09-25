using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using WordPressPCL;
using Editor.Abstractions;
using System.Text.Json.Serialization;

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
        // Primary (swap in tests)
        var primary = _primaryHandlerFactory?.Invoke() ?? new HttpClientHandler();

        // Policy handler (timeouts, retries, error mapping)
        var policy = new Http.PolicyHandler(
            new Editor.Abstractions.ProdRetryPolicy(maxAttempts: 3, baseDelay: TimeSpan.FromMilliseconds(250)),
            perRequestTimeout: _timeout // per-request timeout via linked CTS
        )
        {
            InnerHandler = primary
        };

        // Auth handler outermost so it can set headers before policy runs
        var handler = new AuthDispatchingHandler(GetAuthPreference)
        {
            InnerHandler = policy
        };

        var http = new HttpClient(handler)
        {
            // Keep HttpClient.Timeout for non-WASM; PolicyHandler enforces per-request in all cases
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

    // -----------------------------
    // NEW: Typed call for /users/me
    // -----------------------------
    public async Task<WpMe> GetCurrentUserAsync(CancellationToken ct = default)
    {
        // Ensure HttpClient exists and base address is set
        _ = await GetClientAsync().ConfigureAwait(false);
        var http = HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

        using var res = await http.GetAsync("wp/v2/users/me", ct).ConfigureAwait(false);

        if (res.IsSuccessStatusCode)
        {
            var me = await res.Content.ReadFromJsonAsync<WpMe>(new JsonSerializerOptions(JsonSerializerDefaults.Web), ct)
                     ?? throw new ParseError(bodySnippet: null, message: "Invalid JSON from /users/me");
            return me;
        }

        // PolicyHandler already throws for 401/403 (AuthError), 429 (RateLimited), timeout (TimeoutError),
        // and 5xx (HttpRequestException). If we’re here, it’s a pass-through 4xx like 404/400 → map to domain.
        throw new UnexpectedHttpError(res.StatusCode);
    }
    // -----------------------------
    // Generic JSON POST helper
    // -----------------------------
    private static readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = null,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string NormalizePath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            throw new ArgumentException("Path must not be empty.", nameof(path));

        var p = path.Trim();
        if (p.StartsWith("/", StringComparison.Ordinal)) p = p[1..];
        if (p.StartsWith("wp-json/", StringComparison.OrdinalIgnoreCase)) p = p["wp-json/".Length..];
        return p; // e.g. "wp/v2/posts/123" or "rex/v1/fork"
    }

    public async Task<T?> PostJsonAsync<T>(string path, object body, CancellationToken ct = default)
    {
        _ = await GetClientAsync().ConfigureAwait(false);
        var http = HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

        var normalized = NormalizePath(path);

        using var content = JsonContent.Create(body, options: _json);
        using var res = await http.PostAsync(normalized, content, ct).ConfigureAwait(false);
        res.EnsureSuccessStatusCode();

        if (res.Content.Headers.ContentLength == 0) return default;
        var payload = await res.Content.ReadFromJsonAsync<T>(_json, ct).ConfigureAwait(false);
        if (payload is null) throw new InvalidOperationException($"Invalid JSON from POST {normalized}");
        return payload;
    }
}
