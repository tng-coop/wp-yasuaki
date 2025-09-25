using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Microsoft.JSInterop;
using WordPressPCL;
using WordPressPCL.Models;
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
