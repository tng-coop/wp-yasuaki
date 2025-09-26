using Microsoft.JSInterop;
namespace BlazorWP.Pages;

public partial class Edit
{

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JS.InvokeVoidAsync("setTinyMediaSource",  Flags.WpUrl);
            await JS.InvokeVoidAsync("splitInterop.init", 
                new[] { ".split > .pane:first-child", ".split > .pane:last-child" },
                new { sizes = new[] { 25, 75 } }
            );
        }
    }
}
