using System.Text.Json;
using Microsoft.JSInterop;
using System.Collections.Generic;
using System.Linq;
using WordPressPCL;
using WordPressPCL.Models;
using WordPressPCL.Utility;

namespace BlazorWP.Pages;

public partial class Edit
{
    private async Task SaveDraft()
    {
        //Console.WriteLine("[SaveDraft] starting");

        if (client == null)
        {
            status = "No WordPress endpoint configured.";
            await SaveLocalDraftAsync();
            return;
        }

        var title = postTitle;

        bool success = false;
        try
        {
            if (postId == null)
            {
                var post = new Post
                {
                    Title = new Title(title),
                    Content = new Content(_content),
                    Status = Status.Draft,
                    Categories = selectedCategoryIds.ToList()
                };
                var created = await client.Posts.CreateAsync(post);
                postId = created.Id;
                status = "Draft created";
            }
            else
            {
                var post = new Post
                {
                    Id = postId.Value,
                    Title = new Title(title),
                    Content = new Content(_content),
                    Status = Status.Draft,
                    Categories = selectedCategoryIds.ToList()
                };
                await client.Posts.UpdateAsync(post);
                status = "Draft updated";
            }
            success = true;
        }
        catch (Exception ex)
        {
            status = $"Error: {ex.Message}";
        }

        if (success)
        {
            lastSavedTitle = postTitle;
            lastSavedContent = _content;
            lastSavedCategoryIds = selectedCategoryIds.ToHashSet();
            await RemoveLocalDraftAsync(postId);
            if (postId != null)
            {
                await LoadPostFromServerAsync(postId.Value);
                await InvokeAsync(StateHasChanged);
            }
        }
        else
        {
            await SaveLocalDraftAsync();
        }
        UpdateDirty();
        showRetractReview = false;
        //Console.WriteLine("[SaveDraft] completed");
    }

    private async Task SubmitForReview()
    {
        //Console.WriteLine("[SubmitForReview] starting");
        if (client == null)
        {
            status = "No WordPress endpoint configured.";
            await SaveLocalDraftAsync();
            return;
        }

        var title = postTitle;

        bool success = false;
        try
        {
            if (postId == null)
            {
                var post = new Post
                {
                    Title = new Title(title),
                    Content = new Content(_content),
                    Status = Status.Pending,
                    Categories = selectedCategoryIds.ToList()
                };
                var created = await client.Posts.CreateAsync(post);
                postId = created.Id;
                status = "Submitted for review";
            }
            else
            {
                var post = new Post
                {
                    Id = postId.Value,
                    Title = new Title(title),
                    Content = new Content(_content),
                    Status = Status.Pending,
                    Categories = selectedCategoryIds.ToList()
                };
                await client.Posts.UpdateAsync(post);
                status = "Updated and submitted for review";
            }
            success = true;
        }
        catch (Exception ex)
        {
            status = $"Error: {ex.Message}";
        }

        if (success)
        {
            lastSavedTitle = postTitle;
            lastSavedContent = _content;
            lastSavedCategoryIds = selectedCategoryIds.ToHashSet();
            await RemoveLocalDraftAsync(postId);
            if (postId != null)
            {
                await LoadPostFromServerAsync(postId.Value);
                await InvokeAsync(StateHasChanged);
            }
        }
        else
        {
            await SaveLocalDraftAsync();
        }
        UpdateDirty();
        currentPage = 1;
        hasMore = true;
        await LoadPosts(currentPage);
        showRetractReview = true;
        //Console.WriteLine("[SubmitForReview] completed");
    }

    private async Task RetractReview()
    {
        //Console.WriteLine("[RetractReview] starting");
        if (client == null)
        {
            status = "No WordPress endpoint configured.";
            return;
        }

        if (postId == null)
        {
            status = "No post to retract.";
            return;
        }

        try
        {
            var post = new Post { Id = postId.Value, Status = Status.Draft };
            await client.Posts.UpdateAsync(post);
            status = "Review request retracted";
            showRetractReview = false;
            currentPage = 1;
            hasMore = true;
            await LoadPosts(currentPage);
            await LoadPostFromServerAsync(postId.Value);
            await InvokeAsync(StateHasChanged);
        }
        catch (Exception ex)
        {
            status = $"Error: {ex.Message}";
        }
        //Console.WriteLine("[RetractReview] completed");
    }

    private async Task CloseEditor()
    {
        //Console.WriteLine("[CloseEditor] starting");
        var closedId = postId;
        ResetEditorState();
        await SetEditorContentAsync(_content);
        await RemoveLocalDraftAsync(closedId);
        if (closedId != null)
        {
            var placeholder = posts.FirstOrDefault(p => p.Id == closedId && string.IsNullOrEmpty(p.Status));
            if (placeholder != null)
            {
                posts.Remove(placeholder);
            }
        }
        UpdateDirty();
        await InvokeAsync(StateHasChanged);
        //Console.WriteLine("[CloseEditor] completed");
    }

    private async Task SaveLocalDraftAsync()
    {
        var list = await LoadDraftStatesAsync();
        var existing = list.FirstOrDefault(d => d.PostId == postId);
        if (existing == null)
        {
            existing = new DraftInfo { PostId = postId };
            list.Add(existing);
        }
        existing.Title = postTitle;
        existing.Content = _content;
        existing.Categories = selectedCategoryIds.ToList();
        existing.LastUpdated = DateTime.UtcNow;
        list = list.OrderByDescending(d => d.LastUpdated).Take(3).ToList();
        await SaveDraftStatesAsync(list);
        if (postId == existing.PostId)
        {
            hasPersistedContent = true;
        }
    }

    private async Task<List<DraftInfo>> LoadDraftStatesAsync()
    {
        var json = BrowserStorage.GetItem(DraftsKey);
        if (!string.IsNullOrEmpty(json))
        {
            try
            {
                var list = JsonSerializer.Deserialize<List<DraftInfo>>(json);
                if (list != null) return list;
            }
            catch { }
        }
        return new List<DraftInfo>();
    }

    private async Task SaveDraftStatesAsync(List<DraftInfo> list)
    {
        var json = JsonSerializer.Serialize(list);
        BrowserStorage.SetItem(DraftsKey, json);
    }

    private async Task RemoveLocalDraftAsync(int? id)
    {
        var list = await LoadDraftStatesAsync();
        if (list.RemoveAll(d => d.PostId == id) > 0)
        {
            await SaveDraftStatesAsync(list);
        }
        if (postId == id || postId == null)
        {
            hasPersistedContent = false;
        }
    }
}
