namespace BlazorWP;

public sealed class EndpointState : IEndpointState {
    public Uri? BaseAddress { get; private set; }
    public event Action? Changed;
    public void Set(string root) {
        BaseAddress = new Uri(root.TrimEnd('/') + "/wp-json/");
        Console.WriteLine($"EndpointState: base address set to {BaseAddress}");
        Changed?.Invoke();
    }
}
