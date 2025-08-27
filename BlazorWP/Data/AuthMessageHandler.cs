namespace BlazorWP;

public sealed class AuthMessageHandler : DelegatingHandler
{
    private readonly IAccessGate _gate;
    private readonly IAccessModeService _mode;
    private readonly INonceService _nonce;
    private readonly IJwtService _jwt;

    public AuthMessageHandler(IAccessGate gate, IAccessModeService mode, INonceService nonce, IJwtService jwt)
    {
        _gate = gate; _mode = mode; _nonce = nonce; _jwt = jwt;
        _mode.Changed += _ => _gate.Pause();
        InnerHandler = new HttpClientHandler();
    }

    protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
    {
        await _gate.WaitAsync();

        req.Headers.Remove("Authorization");
        req.Headers.Remove("X-WP-Nonce");

        if (_mode.Mode == AccessMode.Nonce)
        {
            var n = await _nonce.GetAsync(ct);
            Console.WriteLine($"AuthMessageHandler: adding nonce {n}");
            req.Headers.Add("X-WP-Nonce", n);
        }
        else
        {
            var t = await _jwt.GetAsync(ct);
            if (!string.IsNullOrEmpty(t))
            {
                Console.WriteLine("AuthMessageHandler: adding JWT");
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", t);
            }
        }

        var resp = await base.SendAsync(req, ct);
        if (resp.StatusCode == System.Net.HttpStatusCode.Unauthorized)
        {
            Console.WriteLine("AuthMessageHandler: 401 encountered, refreshing");
            if (_mode.Mode == AccessMode.Nonce) await _nonce.RefreshAsync(ct);
            else await _jwt.RefreshAsync(ct);

            req.Headers.Remove("Authorization");
            req.Headers.Remove("X-WP-Nonce");

            if (_mode.Mode == AccessMode.Nonce)
                req.Headers.Add("X-WP-Nonce", await _nonce.GetAsync(ct));
            else
                req.Headers.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", await _jwt.GetAsync(ct));

            resp = await base.SendAsync(req, ct);
        }
        return resp;
    }
}
