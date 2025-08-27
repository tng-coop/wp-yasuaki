using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorWP;

public sealed class WpMediaJsInterop : IAsyncDisposable, IDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public WpMediaJsInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        if (_module == null)
        {
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/wpMedia.js");
        }
        return _module;
    }

    public async ValueTask InitMediaPageAsync(ElementReference iframeEl, ElementReference overlayEl)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("initMediaPage", iframeEl, overlayEl);
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
