using System.Net.Http.Headers;
using Microsoft.AspNetCore.Components;

namespace BlazorWP;

public class AuthMessageHandler : DelegatingHandler
{
    private readonly JwtService _jwtService;
    private readonly INonceService _nonceService;
    private readonly NavigationManager _navManager;

    public AuthMessageHandler(JwtService jwtService, NavigationManager navManager, INonceService nonceService)
    {
        _jwtService = jwtService;
        _nonceService = nonceService;
        _navManager = navManager;
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
        return path.EndsWith("/wp-json/wp/v2", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/wp-json/jwt-auth/v1/token", StringComparison.OrdinalIgnoreCase)
            || path.EndsWith("/jwt-auth/v1/token", StringComparison.OrdinalIgnoreCase);
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
    {
        var uri = new Uri(_navManager.Uri);
        var query = uri.Query.TrimStart('?');
        var parts = query.Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        var useNonce = parts.Any(p => p.Equals("nonce", StringComparison.OrdinalIgnoreCase));

        if (useNonce)
        {
            var nonce = await _nonceService.GetNonceAsync(cancellationToken);
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
            var token = await _jwtService.GetCurrentJwtAsync();
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }

        return await base.SendAsync(request, cancellationToken);
    }
}
