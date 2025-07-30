using Microsoft.JSInterop;

namespace BlazorWP;

public class WpNonceJsInterop : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public WpNonceJsInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        if (_module == null)
        {
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/wpNonce.js");
        }
        return _module;
    }

    public async ValueTask<string?> GetNonceAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string?>("getNonce");
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            await _module.DisposeAsync();
        }
    }
}
