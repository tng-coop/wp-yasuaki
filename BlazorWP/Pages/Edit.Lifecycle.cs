using System.Text.Json;
using System.Linq;
using System.Collections.Generic;
using Microsoft.JSInterop;
using WordPressPCL;
using WordPressPCL.Models;
using WordPressPCL.Utility;

namespace BlazorWP.Pages;

public partial class Edit
{
    protected override async Task OnInitializedAsync()
    {
        //Console.WriteLine("[OnInitializedAsync] starting");
        var draftsJson = await StorageJs.GetItemAsync(DraftsKey);
        DraftInfo? latestDraft = null;
        if (!string.IsNullOrEmpty(draftsJson))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<DraftInfo>>(draftsJson);
                latestDraft = list?.OrderByDescending(d => d.LastUpdated).FirstOrDefault();
            }
            catch
            {
                // ignore deserialization errors
            }
        }

        if (latestDraft != null)
        {
            postId = latestDraft.PostId;
            postTitle = latestDraft.Title ?? string.Empty;
            _content = latestDraft.Content ?? string.Empty;
            lastSavedTitle = postTitle;
            lastSavedContent = _content;
            hasPersistedContent = true;
        }
        else
        {
            ResetEditorState();
            hasPersistedContent = false;
        }

        var trashedSetting = await StorageJs.GetItemAsync(ShowTrashedKey);
        if (!string.IsNullOrEmpty(trashedSetting) && bool.TryParse(trashedSetting, out var trashed))
        {
            showTrashed = trashed;
        }

        await SetupWordPressClientAsync();
        if (client != null)
        {
            try
            {
                var list = await client.Categories.GetAllAsync();
                categories = list?.ToList() ?? new List<Category>();
            }
            catch { }
        }
        currentPage = 1;
        hasMore = true;
        if (!int.TryParse(selectedRefreshCount, out var initCount))
        {
            initCount = 10;
        }
        await LoadPosts(currentPage, perPageOverride: initCount);
        if (postId != null && !posts.Any(p => p.Id == postId))
        {
            posts.Add(new PostSummary
            {
                Id = postId.Value,
                Title = postTitle,
                Author = 0,
                AuthorName = string.Empty,
                Status = string.Empty,
                Date = null
            });
        }
        UpdateDirty();
        //Console.WriteLine("[OnInitializedAsync] completed");
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            //Console.WriteLine("[OnAfterRenderAsync] firstRender");
            mediaSources = new List<string>();
            selectedMediaSource = await StorageJs.GetItemAsync("mediaSource");
            if (string.IsNullOrEmpty(selectedMediaSource))
            {
                selectedMediaSource = Flags.WpUrl;
                if (!string.IsNullOrEmpty(selectedMediaSource))
                {
                    await StorageJs.SetItemAsync("mediaSource", selectedMediaSource);
                }
            }
            if (!string.IsNullOrEmpty(selectedMediaSource))
            {
                await JS.InvokeVoidAsync("setTinyMediaSource", selectedMediaSource);
            }
            if (client != null)
            {
                try
                {
                    var list = await client.Categories.GetAllAsync();
                    categories = list?.ToList() ?? new List<Category>();
                }
                catch { }
            }
            StateHasChanged();
        }
    }

    private async Task SetupWordPressClientAsync()
    {
        client = await Api.GetClientAsync();
    }
}
