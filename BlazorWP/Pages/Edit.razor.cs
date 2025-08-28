using System.Text.Json;
using System.Collections.Generic;
using System.Linq;
using Microsoft.JSInterop;
using WordPressPCL;
using WordPressPCL.Models;
using WordPressPCL.Utility;

namespace BlazorWP.Pages;

public partial class Edit : IDisposable, IAsyncDisposable
{
    private const string DraftsKey = "editorDrafts";
    private const string ShowTrashedKey = "showTrashed";

    private string? status;
    private string postTitle = string.Empty;
    private string lastSavedTitle = string.Empty;
    private string lastSavedContent = string.Empty;
    private bool isDirty = false;
    private bool showRetractReview = false;
    private List<string> mediaSources = new();
    private string? selectedMediaSource;
    private List<Category> categories = new();
    private HashSet<int> selectedCategoryIds = new();
    private HashSet<int> lastSavedCategoryIds = new();
    private List<PostSummary> posts = new();
    private bool hasMore = true;
    private int currentPage = 1;
    private bool isLoading = false;
    private string _content = string.Empty;
    private bool showControls = true;
    private bool showTable = true;
    private bool showTrashed = false;
    private bool hasPersistedContent = false;
    private string selectedRefreshCount = "10";
    private readonly string[] refreshOptions = new[] { "10", "25", "50", "100", "all" };
    private readonly string[] availableStatuses = new[] { "draft", "pending", "publish", "private", "trash" };
    private WordPressClient? client;
    private string? baseUrl;
    private int? postId;

    private IEnumerable<PostSummary> DisplayPosts =>
        posts
            .OrderByDescending(p => p.Date ?? DateTime.MinValue)
            .ThenByDescending(p => p.Id);

    private int DisplayCount => DisplayPosts.Count();

    private string? CurrentPostStatus
    {
        get
        {
            return postId.HasValue
                ? posts.FirstOrDefault(p => p.Id == postId.Value)?.Status
                : null;
        }
    }

    private bool ShowSaveDraftButton
        => postId == null || string.Equals(CurrentPostStatus, "draft", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(CurrentPostStatus);

    private bool ShowSubmitForReviewButton
        => postId == null || string.Equals(CurrentPostStatus, "draft", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(CurrentPostStatus);

    private bool CanSaveDraft => isDirty || hasPersistedContent;

    private bool EditorReadOnly => !ShowSaveDraftButton;

    private static bool IsSelected(PostSummary post, int? selectedId)
    {
        return selectedId != null && post.Id == selectedId;
    }

    private class DraftInfo
    {
        public int? PostId { get; set; }
        public string? Title { get; set; }
        public string? Content { get; set; }
        public List<int> Categories { get; set; } = new();
        public DateTime LastUpdated { get; set; }
    }

    private class PostSummary
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public int Author { get; set; }
        public string? AuthorName { get; set; }
        public string? Status { get; set; }
        public DateTime? Date { get; set; }
        public string? Content { get; set; }
        public List<int> CategoryIds { get; set; } = new();
    }




    public ValueTask DisposeAsync()
    {
        return ValueTask.CompletedTask;
    }

    public void Dispose()
    {
        DisposeAsync().AsTask().GetAwaiter().GetResult();
    }
}
