using Microsoft.JSInterop;

namespace BlazorWP;

public class CredentialManagerJsInterop : IDisposable, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public CredentialManagerJsInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        if (_module == null)
        {
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/credentialManager.js");
        }
        return _module;
    }

    public async ValueTask StoreAsync(string username, string password)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("storeCredentials", username, password);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            await _module.DisposeAsync();
        }
    }

    public void Dispose()
    {
        if (_module != null)
        {
            _module.DisposeAsync().AsTask().GetAwaiter().GetResult();
        }
    }
}
