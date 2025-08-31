using System.Threading;
using System.Threading.Tasks;

namespace BlazorWP;

public interface INonceService
{
    ValueTask<string?> GetNonceAsync(CancellationToken cancellationToken = default);
}

public class NonceService : INonceService
{
    private readonly WpNonceJsInterop _nonceJs;
    private string? _nonce;

    public NonceService(WpNonceJsInterop nonceJs)
    {
        _nonceJs = nonceJs;
    }

    public async ValueTask<string?> GetNonceAsync(CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrEmpty(_nonce))
        {
            _nonce = await _nonceJs.GetNonceAsync();
        }
        return _nonce;
    }
}
