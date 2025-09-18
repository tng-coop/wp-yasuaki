using System.Text.Json;
using Microsoft.JSInterop;
using WordPressPCL;
using WordPressPCL.Models;
using WordPressPCL.Utility;

namespace BlazorWP.Pages;

public partial class Edit
{
    private void ResetEditorState()
    {
        postId = null;
        postTitle = string.Empty;
        _content = string.Empty;
        lastSavedTitle = string.Empty;
        lastSavedContent = string.Empty;
        showRetractReview = false;
        hasPersistedContent = false;
        selectedCategoryIds.Clear();
        lastSavedCategoryIds.Clear();
    }

    private Task SetEditorContentAsync(string html)
    {
        _content = html;
        return Task.CompletedTask;
    }
}
