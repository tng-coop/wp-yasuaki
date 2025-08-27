namespace BlazorWP;

public sealed class AccessInitializer : IAccessInitializer
{
    private readonly IEndpointState _endpoint;
    private readonly IAccessGate _gate;
    private readonly IAccessModeService _mode;
    private readonly INonceService _nonce;
    private readonly IJwtService _jwt;
    private readonly LocalStorageJsInterop _storage;
    private readonly NavigationManager _nav;
    private int _started;

    public AccessInitializer(IEndpointState ep, IAccessGate gate, IAccessModeService mode, INonceService nonce, IJwtService jwt, LocalStorageJsInterop storage, NavigationManager nav)
    {
        _endpoint = ep; _gate = gate; _mode = mode; _nonce = nonce; _jwt = jwt; _storage = storage; _nav = nav;
    }

    public async Task InitializeOnceAsync(CancellationToken ct = default)
    {
        if (Interlocked.Exchange(ref _started, 1) == 1) return;

        Console.WriteLine("AccessInitializer: starting");
        _gate.Pause();

        var root = await LoadEndpointFromStorageOrConfigAsync(ct);
        _endpoint.Set(root);

        if (_mode.Mode == AccessMode.Nonce) await _nonce.TryWarmAsync(ct);
        else await _jwt.TryWarmAsync(ct);

        _gate.Open();
        Console.WriteLine("AccessInitializer: completed");
    }

    private async Task<string> LoadEndpointFromStorageOrConfigAsync(CancellationToken ct)
    {
        var root = await _storage.GetItemAsync("wpEndpoint");
        if (string.IsNullOrWhiteSpace(root))
        {
            root = _nav.BaseUri;
        }
        Console.WriteLine($"AccessInitializer: loaded root {root}");
        return root;
    }
}
