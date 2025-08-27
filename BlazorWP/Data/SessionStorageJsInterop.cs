using Microsoft.JSInterop;

namespace BlazorWP;

public sealed class SessionStorageJsInterop : IAsyncDisposable, IDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public SessionStorageJsInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        if (_module == null)
        {
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/sessionStorageUtils.js");
        }
        return _module;
    }

    public async ValueTask<string?> GetItemAsync(string key)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string?>("getItem", key);
    }

    public async ValueTask SetItemAsync(string key, string value)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("setItem", key, value);
    }

    public async ValueTask DeleteAsync(string key)
    {
        var module = await GetModuleAsync();
       await module.InvokeVoidAsync("deleteItem", key);
    }

    public void Dispose() => _ = DisposeAsync();

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            await _module.DisposeAsync();
        }
    }
}
