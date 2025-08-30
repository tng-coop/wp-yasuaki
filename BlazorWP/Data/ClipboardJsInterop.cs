using Microsoft.JSInterop;

namespace BlazorWP;

public class ClipboardJsInterop : IDisposable, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public ClipboardJsInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        if (_module == null)
        {
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/clipboard.js");
        }
        return _module;
    }

    public async ValueTask CopyAsync(string text)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("copy", text);
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
