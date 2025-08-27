namespace BlazorWP;

public sealed class AuthMessageHandler : DelegatingHandler
{
    private readonly AuthState _auth;

    public AuthMessageHandler(AuthState auth)
    {
        _auth = auth;
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        var token = await _auth.GetAccessTokenAsync(ct);
        if (!string.IsNullOrWhiteSpace(token))
            req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);

        var nonce = await _auth.GetNonceAsync(ct);
        if (!string.IsNullOrWhiteSpace(nonce))
            req.Headers.Add("X-WP-Nonce", nonce);

        return await base.SendAsync(req, ct);
    }
}
