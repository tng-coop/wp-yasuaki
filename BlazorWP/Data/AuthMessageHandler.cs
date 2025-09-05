using System.Net.Http.Headers;
using BlazorWP.Data;

namespace BlazorWP;

public class AuthMessageHandler : DelegatingHandler
{
    private readonly AppPasswordService _appPasswordService;
    private readonly WpNonceJsInterop _nonceJs;
    private readonly AppFlags _flags;

    public AuthMessageHandler(AppPasswordService appPasswordService, WpNonceJsInterop nonceJs, AppFlags flags)
    {
        _appPasswordService = appPasswordService;
        _nonceJs = nonceJs;
        _flags = flags;
        InnerHandler = new HttpClientHandler();
    }

    private static bool ShouldSkipAuth(HttpRequestMessage request)
    {
        var uri = request.RequestUri;
        if (uri == null)
        {
            return false;
        }
        var path = uri.AbsolutePath.TrimEnd('/');
        return path.EndsWith("/wp-json/wp/v2", StringComparison.OrdinalIgnoreCase);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var useNonce = _flags.Auth == AuthType.Nonce;

        if (useNonce)
        {
            var nonce = await _nonceJs.GetNonceAsync();
            if (!string.IsNullOrWhiteSpace(nonce))
            {
                if (!ShouldSkipAuth(request))
                {
                    request.Headers.Remove("Authorization");
                    request.Headers.Remove("X-WP-Nonce");
                    request.Headers.Add("X-WP-Nonce", nonce);
                }
            }
        }
        else if (!ShouldSkipAuth(request))
        {
            var creds = await _appPasswordService.GetAsync();
            if (creds is not null)
            {
                var (u, p) = creds.Value;
                var basic = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{u}:{p}"));
                request.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
