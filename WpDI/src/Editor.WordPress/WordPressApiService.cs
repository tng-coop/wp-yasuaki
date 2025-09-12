using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using WordPressPCL;

namespace Editor.WordPress;

public sealed class WordPressApiService : IWordPressApiService
{
    private readonly HttpClient _http;
    private WordPressClient? _client;

    public WordPressApiService(IOptions<WordPressOptions> options)
    {
        if (options == null) throw new ArgumentNullException(nameof(options));
        var opt = options.Value;
        var handler = new AppPasswordHandler(opt.UserName, opt.AppPassword)
        {
            InnerHandler = new HttpClientHandler()
        };
        _http = new HttpClient(handler)
        {
            Timeout = opt.Timeout
        };
        SetEndpoint(opt.BaseUrl);
    }

    public void SetEndpoint(string endpoint)
    {
        var baseUrl = endpoint.TrimEnd('/') + "/wp-json/";
        _http.BaseAddress = new Uri(baseUrl);
        _http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
        _client = new WordPressClient(_http);
    }

    public Task<WordPressClient?> GetClientAsync() => Task.FromResult(_client);

    public WordPressClient? Client => _client;
    public HttpClient? HttpClient => _http;

    private sealed class AppPasswordHandler : DelegatingHandler
    {
        private readonly string _user;
        private readonly string _pass;

        public AppPasswordHandler(string user, string pass)
        {
            _user = user;
            _pass = pass;
        }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            BasicAuth.Apply(request, _user, _pass);
            return base.SendAsync(request, cancellationToken);
        }
    }
}
