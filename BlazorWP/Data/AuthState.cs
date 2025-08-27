namespace BlazorWP;

public sealed class AuthState
{
    private readonly IAccessModeService _mode;
    private readonly INonceService _nonce;
    private readonly IJwtService _jwt;

    public AuthState(IAccessModeService mode, INonceService nonce, IJwtService jwt)
    {
        _mode = mode;
        _nonce = nonce;
        _jwt = jwt;
    }

    public Task<string?> GetAccessTokenAsync(CancellationToken ct = default) =>
        _mode.Mode == AccessMode.Jwt ? _jwt.GetAsync(ct) : Task.FromResult<string?>(null);

    public async Task<string?> GetNonceAsync(CancellationToken ct = default) =>
        _mode.Mode == AccessMode.Nonce ? await _nonce.GetAsync(ct) : null;
}
