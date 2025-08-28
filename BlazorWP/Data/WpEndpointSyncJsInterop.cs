using Microsoft.JSInterop;

namespace BlazorWP;

public class WpEndpointSyncJsInterop : IDisposable, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public WpEndpointSyncJsInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        if (_module == null)
        {
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/wpEndpointSync.js");
        }
        return _module;
    }

    public async ValueTask RegisterAsync(DotNetObjectReference<object> dotnet)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("register", dotnet);
    }

    public async ValueTask UnregisterAsync()
    {
        if (_module != null)
        {
            await _module.InvokeVoidAsync("unregister");
        }
    }

    public async ValueTask SetAsync(string value)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("set", value);
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
