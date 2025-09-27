using System.ComponentModel.Design;
using Microsoft.JSInterop;
namespace BlazorWP.Pages;


public partial class Edit
{
    private DotNetObjectReference<Edit>? _objRef;
    private IJSObjectReference? _module;
    [JSInvokable]
    public async Task OpenMediaPicker(object? opts)
    {
      if (_mediaPopup is null)
      {
        await JS.InvokeVoidAsync("BlazorBridge.finishPick", (string?)null);
        return;
      }

      var html = await _mediaPopup.OpenAsync(multi: false);
      await JS.InvokeVoidAsync("BlazorBridge.finishPick", html);
    }
    [JSInvokable]
    public Task OnEditorDirtyChanged(bool isDirty)
    {
        _isDirty = isDirty;
        // Console.WriteLine($"Dirty state changed: {isDirty}");
        StateHasChanged();
        return Task.CompletedTask;
    }
    [JSInvokable]
    public async Task OnDraftRestored(string html)
    {
        // optional: log immediately
        // Console.WriteLine($"Draft restore requested, waiting to avoid race");

        // 3 second delay
        await Task.Delay(100);


        if (Content != html)
        {
            // apply recovered content after delay
            Content = html;
            StateHasChanged();
            await JS.InvokeVoidAsync("BlazorBridge.setDirty", "articleEditor", true);
            await Task.Delay(100);
            _isDirty = true;
            StateHasChanged();
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            // ✅ assign to fields (not locals)
            _module = await JS.InvokeAsync<IJSObjectReference>("import", "/js/blazor-bridge.js");
            _objRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("BlazorBridge.init", _objRef);

            await JS.InvokeVoidAsync("setTinyMediaSource", Flags.WpUrl);
        }
    }
    // ✅ Must be public AND [JSInvokable] to be called from JS
    [JSInvokable]
    public async Task OnTinySave(string html)
    {
        // Persist html (e.g., await _service.SaveAsync(html))
        // Console.WriteLine($"Save requested, content length: {html.Length}");
        await SaveAsync();
    }

    // ✅ dispose both the DotNetObjectReference and JS module
    public async ValueTask DisposeAsync()
    {
        _objRef?.Dispose();
        if (_module is not null)
        {
            try { await _module.DisposeAsync(); } catch { /* no-op */ }
        }
    }
}
