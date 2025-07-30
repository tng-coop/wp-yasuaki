using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;

namespace BlazorWP;

public class WpMediaJsInterop : IAsyncDisposable
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

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            await _module.DisposeAsync();
        }
    }
}
