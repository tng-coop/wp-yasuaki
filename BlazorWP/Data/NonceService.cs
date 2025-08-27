namespace BlazorWP;

public sealed class NonceService : INonceService {
    private readonly WpNonceJsInterop _nonceJs;
    private string? _nonce;
    public NonceService(WpNonceJsInterop nonceJs) {
        _nonceJs = nonceJs;
    }
    public async Task<string> GetAsync(CancellationToken ct = default) {
        if (string.IsNullOrEmpty(_nonce)) {
            _nonce = await _nonceJs.GetNonceAsync();
            Console.WriteLine($"NonceService: loaded {_nonce}");
        }
        return _nonce ?? string.Empty;
    }
    public async Task RefreshAsync(CancellationToken ct = default) {
        Console.WriteLine("NonceService: refresh");
        _nonce = await _nonceJs.GetNonceAsync();
    }
    public async Task TryWarmAsync(CancellationToken ct = default) {
        Console.WriteLine("NonceService: warm");
        _nonce = await _nonceJs.GetNonceAsync();
    }
    public Task ClearAsync(CancellationToken ct = default) {
        Console.WriteLine("NonceService: clear");
        _nonce = null;
        return Task.CompletedTask;
    }
}
