using Microsoft.JSInterop;
namespace BlazorWP.Pages;


public partial class Edit
{
    private DotNetObjectReference<Edit>? _objRef;

    // called from JS: window.BlazorDirtyBridge.report(...)
    [JSInvokable]
    public Task OnEditorDirtyChanged(bool isDirty)
    {
        _isDirty = isDirty;
        Console.WriteLine($"Dirty state changed: {isDirty}");
        StateHasChanged();
        return Task.CompletedTask;
    }
    [JSInvokable]
    public async Task OnDraftRestored(string html)
    {
        // optional: log immediately
        Console.WriteLine($"Draft restore requested, waiting to avoid race");

        // 3 second delay
        await Task.Delay(100);

        // apply recovered content after delay
        Content = html;
        Console.WriteLine($"Draft restored, content length: {html.Length}");

        StateHasChanged();
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            var mod = await JS.InvokeAsync<IJSObjectReference>("import", "/js/blazor-bridge.js");
            var objRef = DotNetObjectReference.Create(this);
            await JS.InvokeVoidAsync("BlazorBridge.init", objRef);

            await JS.InvokeVoidAsync("setTinyMediaSource", Flags.WpUrl);
            await JS.InvokeVoidAsync("splitInterop.init",
                new[] { ".split > .pane:first-child", ".split > .pane:last-child" },
                new { sizes = new[] { 25, 75 }, direction = "vertical" }
            );
        }
    }
    [JSInvokable]
    public Task OnEditorSave() => SaveAsync();   // your existing save

    public ValueTask DisposeAsync()
    {
        _objRef?.Dispose();
        return ValueTask.CompletedTask;
    }

}
