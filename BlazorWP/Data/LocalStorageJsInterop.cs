using Microsoft.JSInterop;

namespace BlazorWP;

public class LocalStorageJsInterop : IDisposable, IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public LocalStorageJsInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        if (_module == null)
        {
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/storageUtils.js");
        }
        return _module;
    }

    public async ValueTask<string[]> KeysAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string[]>("keys");
    }

    public async ValueTask<LocalStorageItemInfo> ItemInfoAsync(string key)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<LocalStorageItemInfo>("itemInfo", key);
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

    public class LocalStorageItemInfo
    {
        public string? Value { get; set; }
        public string? LastUpdated { get; set; }
    }
}
