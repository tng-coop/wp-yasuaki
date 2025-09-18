using System.Text.Json;
using Microsoft.JSInterop;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using WordPressPCL;
using WordPressPCL.Models;
using WordPressPCL.Utility;

namespace BlazorWP.Pages;

public partial class Edit
{
    private async Task LoadPosts(int page = 1, bool append = false, int? perPageOverride = null)
    {
        //Console.WriteLine($"[LoadPosts] page={page}, append={append}");
        if (page == 1 && !append)
        {
            posts.Clear();
        }
        if (client == null)
        {
            return;
        }

        var statuses = new List<Status>
        {
            Status.Publish,
            Status.Private,
            Status.Draft,
            Status.Pending,
            Status.Future
        };
        if (showTrashed)
        {
            statuses.Add(Status.Trash);
        }

        var qb = new PostsQueryBuilder
        {
            Context = Context.Edit,
            Page = page,
            PerPage = perPageOverride ?? (page == 1 && !append ? 10 : 20),
            Embed = true,
            Statuses = statuses,
            OrderBy = PostsOrderBy.Date,
            Order = Order.DESC
        };

        try
        {
            var list = await client.Posts.QueryAsync(qb, useAuth: true);
            int count = 0;
            foreach (var p in list)
            {
                posts.Add(new PostSummary
                {
                    Id = p.Id,
                    Title = p.Title?.Rendered ?? string.Empty,
                    Author = p.Author,
                    AuthorName = p.Embedded?.Author?.FirstOrDefault()?.Name,
                    Status = p.Status.ToString().ToLowerInvariant(),
                    Date = DateTime.SpecifyKind(p.DateGmt, DateTimeKind.Utc).ToLocalTime(),
                    Content = p.Content?.Rendered,
                    CategoryIds = p.Categories ?? new List<int>()
                });
                count++;
            }
            //Console.WriteLine($"[LoadPosts] loaded {count} posts");
            hasMore = count > 0;
        }
        catch
        {
            //Console.WriteLine("[LoadPosts] error fetching posts");
            hasMore = false;
        }

        showRetractReview = posts?.FirstOrDefault(p => p.Id == postId)?.Status == "pending";
        await InvokeAsync(StateHasChanged);
    }

    private async Task PullMore()
    {
        if (isLoading || !hasMore) return;
        isLoading = true;
        currentPage++;
        await LoadPosts(currentPage, append: true);
        isLoading = false;
    }

    private async Task<bool> TryLoadDraftAsync(int? id)
    {
        //Console.WriteLine($"[TryLoadDraftAsync] id={id}");
        var list = await LoadDraftStatesAsync();
        var item = list.FirstOrDefault(d => d.PostId == id);
        if (item != null)
        {
            //Console.WriteLine("[TryLoadDraftAsync] draft found");
            postId = item.PostId;
            postTitle = item.Title ?? string.Empty;
            _content = item.Content ?? string.Empty;
            //Console.WriteLine($"[TryLoadDraftAsync] loaded draft title length={postTitle.Length}, content length={_content.Length}");
            lastSavedTitle = postTitle;
            lastSavedContent = _content;
            selectedCategoryIds = item.Categories != null
                ? item.Categories.ToHashSet()
                : new HashSet<int>();
            lastSavedCategoryIds = selectedCategoryIds.ToHashSet();
            await SetEditorContentAsync(_content);
            if (postId != null && !posts.Any(p => p.Id == postId))
            {
                posts.Add(new PostSummary
                {
                    Id = postId.Value,
                    Title = postTitle,
                    Author = 0,
                    AuthorName = string.Empty,
                    Status = string.Empty,
                    Date = null,
                    Content = _content
                });
            }
            hasPersistedContent = true;
            return true;
        }
        //Console.WriteLine("[TryLoadDraftAsync] no draft found");
        hasPersistedContent = false;
        return false;
    }

    private async Task LoadPostFromServerAsync(int id)
    {
        //Console.WriteLine($"[LoadPostFromServerAsync] id={id}");
        if (client == null)
        {
            status = "No WordPress endpoint configured.";
            return;
        }

        try
        {
            var post = await client.Posts.GetByIDAsync(id, true, true);
            postId = id;
            postTitle = post.Title?.Rendered ?? string.Empty;
            _content = post.Content?.Rendered ?? string.Empty;
            //Console.WriteLine($"[LoadPostFromServerAsync] loaded title length={postTitle.Length}, content length={_content.Length}");
            lastSavedTitle = postTitle;
            lastSavedContent = _content;
            selectedCategoryIds = post.Categories != null
                ? post.Categories.ToHashSet()
                : new HashSet<int>();
            lastSavedCategoryIds = selectedCategoryIds.ToHashSet();
            await SetEditorContentAsync(_content);
            hasPersistedContent = false;
            var existing = posts.FirstOrDefault(p => p.Id == id);
            if (existing == null)
            {
                posts.Add(new PostSummary
                {
                    Id = post.Id,
                    Title = post.Title?.Rendered ?? postTitle,
                    Author = post.Author,
                    AuthorName = post.Embedded?.Author?.FirstOrDefault()?.Name ?? string.Empty,
                    Status = post.Status.ToString().ToLowerInvariant(),
                    Date = DateTime.SpecifyKind(post.DateGmt, DateTimeKind.Utc).ToLocalTime(),
                    Content = post.Content?.Rendered,
                    CategoryIds = post.Categories ?? new List<int>()
                });
            }
            else
            {
                existing.Title = post.Title?.Rendered ?? postTitle;
                existing.Author = post.Author;
                existing.AuthorName = post.Embedded?.Author?.FirstOrDefault()?.Name ?? string.Empty;
                existing.Status = post.Status.ToString().ToLowerInvariant();
                existing.Date = DateTime.SpecifyKind(post.DateGmt, DateTimeKind.Utc).ToLocalTime();
                existing.Content = post.Content?.Rendered;
                existing.CategoryIds = post.Categories ?? new List<int>();
            }
        }
        catch (Exception ex)
        {
            status = $"Error: {ex.Message}";
            //Console.WriteLine($"[LoadPostFromServerAsync] exception: {ex.Message}");
        }
    }

    private async Task OpenPost(PostSummary post, bool forceReload = false)
    {
        var sw = Stopwatch.StartNew();
        //Console.WriteLine($"[OpenPost] click id={post.Id}, title={post.Title}");
        if (post.Id == postId && !forceReload)
        {
            sw.Stop();
            Console.WriteLine($"[Perf] OpenPost({post.Id}) took {sw.ElapsedMilliseconds} ms (noop)");
            return;
        }

        if (isDirty)
        {
            await SaveLocalDraftAsync();
            //Console.WriteLine("[OpenPost] autosaved dirty draft");
        }

        if (!await TryLoadDraftAsync(post.Id))
        {
            if (post.Id > 0)
            {
                if (!forceReload && !string.IsNullOrEmpty(post.Content))
                {
                    postId = post.Id;
                    postTitle = post.Title ?? string.Empty;
                    _content = post.Content;
                    lastSavedTitle = postTitle;
                    lastSavedContent = _content;
                    selectedCategoryIds = post.CategoryIds.ToHashSet();
                    lastSavedCategoryIds = selectedCategoryIds.ToHashSet();
                }
                else
                {
                    //Console.WriteLine("[OpenPost] loading from server");
                    await LoadPostFromServerAsync(post.Id);
                }
            }
            else
            {
                //Console.WriteLine("[OpenPost] new empty post");
                ResetEditorState();
                await SetEditorContentAsync(_content);
            }
        }

        showRetractReview = post.Status == "pending";
        UpdateDirty();
        //Console.WriteLine($"[OpenPost] completed. postId={postId}");
        await InvokeAsync(StateHasChanged);
        sw.Stop();
        Console.WriteLine($"[Perf] OpenPost({post.Id}) took {sw.ElapsedMilliseconds} ms");
    }

    private async Task RefreshPosts()
    {
        if (isLoading) return;
        isLoading = true;
        currentPage = 1;
        hasMore = true;

        if (string.Equals(selectedRefreshCount, "all", StringComparison.OrdinalIgnoreCase))
        {
            int page = 1;
            bool first = true;
            while (true)
            {
                await LoadPosts(page, append: !first, perPageOverride: 100);
                first = false;
                if (!hasMore)
                {
                    break;
                }
                page++;
            }
        }
        else
        {
            if (!int.TryParse(selectedRefreshCount, out var count))
            {
                count = 10;
            }
            await LoadPosts(1, append: false, perPageOverride: count);
        }
        isLoading = false;
    }

}
