using Microsoft.JSInterop;
namespace BlazorWP.Pages;


public partial class Edit
{
    private DotNetObjectReference<Edit>? _objRef;

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
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
