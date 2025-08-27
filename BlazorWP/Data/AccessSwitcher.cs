namespace BlazorWP;

public sealed class AccessSwitcher : IAccessSwitcher
{
    private readonly IAccessGate _gate;
    private readonly IAccessModeService _mode;
    private readonly IEndpointState _endpoint;
    private readonly INonceService _nonce;
    private readonly IJwtService _jwt;

    public AccessSwitcher(IAccessGate gate, IAccessModeService mode, IEndpointState ep, INonceService nonce, IJwtService jwt)
    {
        _gate = gate; _mode = mode; _endpoint = ep; _nonce = nonce; _jwt = jwt;
    }

    public async Task SwitchToAsync(AccessMode target, string? newRootUrl = null, CancellationToken ct = default)
    {
        if (_mode.Mode == target && newRootUrl is null) return;

        Console.WriteLine($"AccessSwitcher: switching to {target}");
        _gate.Pause();

        if (newRootUrl is not null) _endpoint.Set(newRootUrl);

        if (target == AccessMode.Nonce) await _jwt.ClearAsync(ct);
        else await _nonce.ClearAsync(ct);

        _mode.Set(target);

        if (target == AccessMode.Nonce) await _nonce.TryWarmAsync(ct);
        else await _jwt.TryWarmAsync(ct);

        _gate.Open();
    }
}
