namespace BlazorWP;

public sealed class AccessModeService : IAccessModeService {
    public AccessMode Mode { get; private set; } = AccessMode.Nonce;
    public event Action<AccessMode>? Changed;
    public void Set(AccessMode mode) {
        if (Mode == mode) return;
        Mode = mode;
        Console.WriteLine($"AccessModeService: mode changed to {mode}");
        Changed?.Invoke(mode);
    }
}
