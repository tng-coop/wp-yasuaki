using Microsoft.JSInterop;

namespace BlazorWP;

public record PdfRenderInfo(int Width, int Height, string DataUrl);

public class UploadPdfJsInterop : IAsyncDisposable
{
    private readonly IJSRuntime _jsRuntime;
    private IJSObjectReference? _module;

    public UploadPdfJsInterop(IJSRuntime jsRuntime)
    {
        _jsRuntime = jsRuntime;
    }

    private async ValueTask<IJSObjectReference> GetModuleAsync()
    {
        if (_module == null)
        {
            _module = await _jsRuntime.InvokeAsync<IJSObjectReference>("import", "./js/upload-pdf.js");
        }
        return _module;
    }

    public async ValueTask InitializeAsync(string imgId)
    {
        var module = await GetModuleAsync();
        await module.InvokeVoidAsync("initialize", imgId);
    }

    public async ValueTask<PdfRenderInfo> RenderFirstPageAsync(DotNetStreamReference streamRef, string canvasId, string imgId)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<PdfRenderInfo>("renderFirstPageFromStream", streamRef, canvasId, imgId);
    }

    public async ValueTask<string?> GetCurrentDataUrlAsync()
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string?>("getCurrentDataUrl");
    }

    public async ValueTask<string?> GetScaledPreviewAsync(int width, int height, bool cover)
    {
        var module = await GetModuleAsync();
        return await module.InvokeAsync<string?>("getScaledPreview", width, height, cover);
    }

    public async ValueTask DisposeAsync()
    {
        if (_module != null)
        {
            await _module.DisposeAsync();
        }
    }
}
