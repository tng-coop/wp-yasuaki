namespace BlazorWP;

public enum AccessMode { Nonce, Jwt }

public interface IAccessModeService {
    AccessMode Mode { get; }
    event Action<AccessMode> Changed;
    void Set(AccessMode mode);
}

public interface IEndpointState {
    Uri? BaseAddress { get; }
    event Action Changed;
    void Set(string rootUrl);
}

public interface IAccessGate { Task WaitAsync(); void Pause(); void Open(); }

public interface INonceService {
    Task<string> GetAsync(CancellationToken ct = default);
    Task RefreshAsync(CancellationToken ct = default);
    Task TryWarmAsync(CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}

public interface IJwtService {
    Task<string?> GetAsync(CancellationToken ct = default);
    Task RefreshAsync(CancellationToken ct = default);
    Task TryWarmAsync(CancellationToken ct = default);
    Task ClearAsync(CancellationToken ct = default);
}

public interface IAccessInitializer { Task InitializeOnceAsync(CancellationToken ct = default); }

public interface IAccessSwitcher {
    Task SwitchToAsync(AccessMode mode, string? newRootUrl = null, CancellationToken ct = default);
}
