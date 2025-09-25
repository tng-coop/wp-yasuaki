using Microsoft.JSInterop;
namespace BlazorWP.Pages;

public partial class Edit
{

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JS.InvokeVoidAsync("setTinyMediaSource",  Flags.WpUrl);
        }
    }
}
