using System.Text.Json;
using Microsoft.JSInterop;
using Microsoft.AspNetCore.Components;
using System.Collections.Generic;
using System.Linq;
using WordPressPCL;
using WordPressPCL.Models;
using WordPressPCL.Utility;

namespace BlazorWP.Pages;

public partial class Edit
{
    private async Task OnMediaSourceChanged()
    {
        if (string.IsNullOrEmpty(selectedMediaSource))
        {
        BrowserStorage.DeleteItem("mediaSource");
        }
        else
        {
        BrowserStorage.SetItem("mediaSource", selectedMediaSource);
        }
        //Console.WriteLine($"[OnMediaSourceChanged] source='{selectedMediaSource}'");
        await JS.InvokeVoidAsync("setTinyMediaSource", selectedMediaSource);
    }

    private void OnTitleChanged()
    {
        //Console.WriteLine($"[OnTitleChanged] length={postTitle.Length}");
        UpdateDirty();
    }

    [JSInvokable]
    public async Task OnEditorBlur()
    {
        await SaveLocalDraftAsync();
        lastSavedContent = _content;
        UpdateDirty();
    }

    [JSInvokable]
    public void OnEditorDirty()
    {
        isDirty = true;
    }

    private void UpdateDirty()
    {
        isDirty = postTitle != lastSavedTitle ||
            _content != lastSavedContent ||
            !selectedCategoryIds.SetEquals(lastSavedCategoryIds);
        //Console.WriteLine($"[UpdateDirty] isDirty={isDirty}");
    }

    private void ToggleControls()
    {
        showControls = !showControls;
    }

    private Task ToggleTable()
    {
        showTable = !showTable;
        return Task.CompletedTask;
    }

    private async Task OnShowTrashedChanged()
    {
        BrowserStorage.SetItem(ShowTrashedKey, showTrashed.ToString().ToLowerInvariant());
        // Only update the stored preference. Actual querying happens when
        // the user explicitly clicks the Refresh button.
    }

    private void OnCategoryCheckboxChanged(int id, ChangeEventArgs e)
    {
        var isChecked = e.Value as bool? == true;
        if (isChecked)
        {
            selectedCategoryIds.Add(id);
        }
        else
        {
            selectedCategoryIds.Remove(id);
        }
        UpdateDirty();
    }

    private async Task ChangeStatus(PostSummary post, string newStatus)
    {
        if (post.Id <= 0) return;

        if (client == null)
        {
            status = "No WordPress endpoint configured.";
            return;
        }

        try
        {
            if (string.Equals(newStatus, "trash", StringComparison.OrdinalIgnoreCase))
            {
                await client.Posts.DeleteAsync(post.Id, false);
                status = "Post moved to trash";
                posts.Remove(post);
            }
            else
            {
                var postUpdate = new Post { Id = post.Id, Status = Enum.TryParse<Status>(newStatus, true, out var st) ? st : Status.Draft };
                await client.Posts.UpdateAsync(postUpdate);
                status = $"Status changed to {newStatus}";
                post.Status = newStatus;
            }
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            status = $"Error: {ex.Message}";
        }
    }

    private async Task NewPost()
    {
        if (postId != null)
        {
            if (isDirty)
            {
                await SaveLocalDraftAsync();
            }
        }
        else
        {
            var list = await LoadDraftStatesAsync();
            list.RemoveAll(d => d.PostId == null);
            await SaveDraftStatesAsync(list);
        }

        if (!await TryLoadDraftAsync(null))
        {
            ResetEditorState();
            await SetEditorContentAsync(_content);
        }

        showRetractReview = false;
        UpdateDirty();
        await InvokeAsync(StateHasChanged);
    }

    private static string GetStatusButtonClass(string? status)
    {
        return status switch
        {
            "draft" => "btn-outline-secondary",
            "pending" => "btn-outline-warning",
            "publish" => "btn-outline-success",
            "private" => "btn-outline-dark",
            "trash" => "btn-outline-danger",
            _ => "btn-outline-secondary",
        };
    }
}
