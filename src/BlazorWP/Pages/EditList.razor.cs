using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Editor.WordPress;
using Microsoft.AspNetCore.Components;

// WordPressPCL
using WordPressPCL;
using WordPressPCL.Models;
// If you have the exceptions package/version available, uncomment the next line
// using WordPressPCL.Exceptions;

namespace BlazorWP.Pages;

public partial class EditList : IDisposable
{
    // From parent Edit.razor
    [Parameter] public int? SelectedId { get; set; }

    [Inject] private NavigationManager Nav { get; set; } = default!;
    [Inject] public IWordPressApiService Api { get; set; } = default!;

    private sealed class RenderWrapper { public string? rendered { get; set; } public string? raw { get; set; } }
    private sealed class PostListItem
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
        public string? Status { get; set; }
        public string? ModifiedGmt { get; set; }
    }

    private readonly List<PostListItem> _posts = new();
    private bool _loadingPosts;
    private string? _search;
    private string? _error;

    private CancellationTokenSource? _loadCts;

    private const int PerPage = 50;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    protected override async Task OnInitializedAsync() => await LoadPostsAsync();

    private Task SearchAsync() => LoadPostsAsync();
    private Task RefreshPosts() => LoadPostsAsync();

    private void NavigateToPost(int id)
    {
        if (id > 0) Nav.NavigateTo($"edit/{id}");
    }

    private static string FormatLocal(string? modifiedGmt)
    {
        if (string.IsNullOrWhiteSpace(modifiedGmt)) return "";
        if (DateTimeOffset.TryParse(
                modifiedGmt,
                CultureInfo.InvariantCulture,
                DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal,
                out var utc))
        {
            return utc.ToLocalTime().ToString("yyyy-MM-dd HH:mm");
        }
        return modifiedGmt!;
    }

    private async Task LoadPostsAsync()
    {
        // Cancel any in-flight request to avoid overlapping loads
        _loadCts?.Cancel();
        _loadCts = new CancellationTokenSource();
        var ct = _loadCts.Token;

        _loadingPosts = true;
        _error = null;

        try
        {
            _posts.Clear();
            var wp = await Api.GetClientAsync()
                     ?? throw new InvalidOperationException("WordPress client is not initialized.");


            var baseRoute = $"wp/v2/posts?context=edit&_fields=id,title,modified_gmt,status&per_page={PerPage}";
            if (!string.IsNullOrWhiteSpace(_search))
                baseRoute += "&search=" + Uri.EscapeDataString(_search.Trim());

            // Preserve your original "any" behavior; if rejected, fall back to explicit statuses
            var statuses = "any";
            var page = 1;

            while (true)
            {
                ct.ThrowIfCancellationRequested();

                var route = $"{baseRoute}&status={statuses}&page={page}";

                IList<Post>? pagePosts = null;

                try
                {
                    // Uses the same HttpClient/auth as the WordPress client
                    pagePosts = await wp.CustomRequest.GetAsync<IList<Post>>(route);
                }
                catch (Exception ex) // WPException/HttpRequestException/etc.
                {
                    // If server rejects "any", retry once with explicit statuses
                    if (statuses == "any")
                    {
                        statuses = "publish,draft,pending,private,future";
                        continue; // retry this same page with explicit statuses
                    }
                    // Otherwise rethrow; it's a real error (auth, network, etc.)
                    throw new InvalidOperationException($"Failed to load posts: {ex.Message}", ex);
                }

                if (pagePosts is null || pagePosts.Count == 0)
                    break;

                foreach (var p in pagePosts)
                {
                    // Title: prefer Raw (needs context=edit), then Rendered; guard before Trim()
                    var titleRaw = p.Title?.Raw ?? p.Title?.Rendered;
                    var title = string.IsNullOrWhiteSpace(titleRaw) ? "(untitled)" : titleRaw.Trim();

                    // ModifiedGmt: DateTime (non-nullable) in this WPCL version
                    var modifiedGmt = p.ModifiedGmt.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");

                    _posts.Add(new PostListItem
                    {
                        Id = p.Id,
                        Title = title,
                        Status = p.Status.ToString().ToLowerInvariant(), // enum -> "publish", "draft", etc.
                        ModifiedGmt = modifiedGmt
                    });
                }


                // No header access via CustomRequest; use a count heuristic
                if (pagePosts.Count < PerPage)
                    break;

                page++;
            }

            // Ensure newest-first if the API returns unsorted data
            _posts.Sort((a, b) =>
            {
                static DateTime ParseOrMin(string? s) =>
                    DateTime.TryParse(s, out var dt) ? dt : DateTime.MinValue;
                return ParseOrMin(b.ModifiedGmt).CompareTo(ParseOrMin(a.ModifiedGmt));
            });

        }
        catch (OperationCanceledException)
        {
            // Swallow; superseded by a newer request
        }
        catch (Exception ex)
        {
            _error = ex.Message;
        }
        finally
        {
            _loadingPosts = false;
            StateHasChanged();
        }
    }

    public void Dispose()
    {
        _loadCts?.Cancel();
        _loadCts?.Dispose();
    }
}
