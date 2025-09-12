using BlazorWP.Data;
using Microsoft.AspNetCore.Components.WebAssembly.Http;
using Editor.WordPress;

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

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var useNonce = _flags.Auth == AuthType.Nonce;

        if (useNonce)
        {
            var nonce = await _nonceJs.GetNonceAsync();
            if (!string.IsNullOrWhiteSpace(nonce))
            {
                if (!BasicAuth.ShouldSkip(request))
                {
                    request.Headers.Remove("Authorization");
                    request.Headers.Remove("X-WP-Nonce");
                    request.Headers.Add("X-WP-Nonce", nonce);
                }
            }
        }
        else if (!BasicAuth.ShouldSkip(request))
        {
            var creds = await _appPasswordService.GetAsync();
            if (creds is not null)
            {
                var (u, p) = creds.Value;
                BasicAuth.Apply(request, u, p);
                // 👇 prevent cookies from being sent when using AppPass
                request.SetBrowserRequestCredentials(BrowserRequestCredentials.Omit);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
