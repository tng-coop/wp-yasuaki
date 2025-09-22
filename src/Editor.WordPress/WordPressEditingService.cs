// WordPressEditingService.cs
#nullable enable
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

using Editor.Abstractions;   // IPostEditor etc. live here in your repo
using WordPressPCL;          // you already reference this
using Editor.WordPress;      // IWordPressApiService, IEditLockService live here

// -------------------------------
// 1) DTOs + Interface (minimal)
// -------------------------------
namespace Editor.Abstractions
{
    public record Paged<T>(IReadOnlyList<T> Items, int Page, int PerPage, int? Total, int? TotalPages);

    // Keep ModifiedUtc as the WP "modified_gmt" string so we can pass it back verbatim for conflict detection
    public record PostSummary(
        long Id,
        string Title,
        string Status,
        string? ModifiedUtc,
        string? Author,
        DateTimeOffset? Date,
        IReadOnlyList<int> CategoryIds);

    public record PostDetail(
        long Id,
        string Title,
        string Html,
        string Status,
        IReadOnlyList<int> CategoryIds,
        string? ModifiedUtc,
        string? Link);

    public enum SaveOutcome { Created, Updated, Submitted, Published, Recovered, Conflict, AuthRequired, RateLimited, Error }

    public record SaveResult(
        SaveOutcome Outcome,
        long PostId,
        string Status,
        DateTimeOffset? ServerModifiedUtc,
        string? Message = null);

    public enum LockState { Acquired, AlreadyLocked, Released, NotHeld, Failed }

    public record LockResult(LockState State, long? OtherUserId = null, string? Message = null);

    public record CategoryInfo(int Id, string Name, int? ParentId);

    /// <summary>Thin, intent-level façade the UI calls. One class implements this whole surface.</summary>
    public interface IEditingService
    {
        Task<Paged<PostSummary>> ListPostsAsync(
            int page = 1, int perPage = 20,
            string? search = null, string? statusCsv = "draft,pending,publish,private",
            int? categoryId = null, CancellationToken ct = default);

        Task<PostDetail?> GetPostAsync(long id, CancellationToken ct = default);

        // Server autosave: creates a non-public revision; live post stays the same
        Task<SaveResult> AutosaveAsync(PostDetail post, CancellationToken ct = default);

        // Save/submit/publish (standard WP statuses). Uses ModifiedUtc to detect conflicts.
        Task<SaveResult> SaveDraftAsync(PostDetail post, CancellationToken ct = default);
        Task<SaveResult> SubmitForReviewAsync(PostDetail post, CancellationToken ct = default);
        Task<SaveResult> PublishAsync(PostDetail post, CancellationToken ct = default);

        // Unpublish
        Task<SaveResult> SwitchToDraftAsync(long id, CancellationToken ct = default);

        // Locking
        Task<LockResult> AcquireLockAsync(long postId, CancellationToken ct = default);
        Task<LockResult> ReleaseLockAsync(CancellationToken ct = default);

        // Categories (search/paging for large taxonomies)
        Task<IReadOnlyList<CategoryInfo>> ListCategoriesAsync(
            string? search = null, int page = 1, int perPage = 100, int? parentId = null, CancellationToken ct = default);
    }
}

// --------------------------------------
// 2) Implementation over your WP stack
// --------------------------------------
namespace Editor.WordPress
{
    using static System.StringComparison;

    public sealed class WordPressEditingService : IEditingService
    {
        private readonly IWordPressApiService _api;
        private readonly IPostEditor _postEditor;
        private readonly IEditLockService _locks;

        private IEditLockSession? _lockSession;
        private long? _lockedPostId;

        private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

        public WordPressEditingService(IWordPressApiService api, IPostEditor postEditor, IEditLockService locks)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _postEditor = postEditor ?? throw new ArgumentNullException(nameof(postEditor));
            _locks = locks ?? throw new ArgumentNullException(nameof(locks));
        }

        // -------------------
        // Listing / Reading
        // -------------------
        public async Task<Paged<Editor.Abstractions.PostSummary>> ListPostsAsync(
            int page = 1, int perPage = 20,
            string? search = null, string? statusCsv = "draft,pending,publish,private",
            int? categoryId = null, CancellationToken ct = default)
        {
            _ = await _api.GetClientAsync().ConfigureAwait(false);
            var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient not initialized.");

            var qs = new StringBuilder($"wp/v2/posts?context=edit&_embed=1&per_page={perPage}&page={page}");
            if (!string.IsNullOrWhiteSpace(search)) qs.Append("&search=").Append(Uri.EscapeDataString(search));
            if (!string.IsNullOrWhiteSpace(statusCsv)) qs.Append("&status=").Append(Uri.EscapeDataString(statusCsv));
            if (categoryId is not null) qs.Append("&categories=").Append(categoryId.Value);

            using var res = await http.GetAsync(qs.ToString(), ct).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();

            var items = new List<Editor.Abstractions.PostSummary>();
            using (var doc = JsonDocument.Parse(body))
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var id = el.TryGetProperty("id", out var idEl) ? idEl.GetInt64() : 0;
                    var status = el.TryGetProperty("status", out var sEl) ? sEl.GetString() ?? "" : "";
                    var link = el.TryGetProperty("link", out var lEl) ? lEl.GetString() : null;

                    string title = el.TryGetProperty("title", out var tEl) && tEl.TryGetProperty("rendered", out var tr)
                        ? (tr.GetString() ?? "")
                        : "";

                    string? modifiedUtc = el.TryGetProperty("modified_gmt", out var mg) ? mg.GetString() : null;
                    DateTimeOffset? date = null;
                    if (el.TryGetProperty("date_gmt", out var dg) && DateTimeOffset.TryParse(dg.GetString(), out var dto))
                        date = dto;

                    List<int> cats = new();
                    if (el.TryGetProperty("categories", out var ce) && ce.ValueKind == JsonValueKind.Array)
                        foreach (var cid in ce.EnumerateArray())
                            cats.Add(cid.GetInt32());

                    string? author = null;
                    if (el.TryGetProperty("_embedded", out var emb) &&
                        emb.TryGetProperty("author", out var authArr) &&
                        authArr.ValueKind == JsonValueKind.Array &&
                        authArr.GetArrayLength() > 0)
                    {
                        var a0 = authArr[0];
                        if (a0.TryGetProperty("name", out var nameEl)) author = nameEl.GetString();
                    }

                    items.Add(new Editor.Abstractions.PostSummary(id, title, status, modifiedUtc, author, date, cats));
                }
            }

            int? total = TryGetIntHeader(res, "X-WP-Total");
            int? totalPages = TryGetIntHeader(res, "X-WP-TotalPages");

            return new Editor.Abstractions.Paged<Editor.Abstractions.PostSummary>(items, page, perPage, total, totalPages);
        }

        public async Task<Editor.Abstractions.PostDetail?> GetPostAsync(long id, CancellationToken ct = default)
        {
            var client = await _api.GetClientAsync().ConfigureAwait(false);
            if (client is null) throw new InvalidOperationException("WordPress client not initialized.");

            // Ask for edit context so we can get raw fields where possible.
            var post = await client.Posts.GetByIDAsync((int)id, embed: true, useAuth: true).ConfigureAwait(false);
            if (post is null) return null;

            // Prefer raw content if present, fallback to rendered
            var raw = post.Content?.Raw ?? post.Content?.Rendered ?? "";
            var cats = post.Categories ?? new List<int>();
            var authorName = post.Embedded?.Author?.FirstOrDefault()?.Name;

            // FIX: ModifiedGmt is non-nullable DateTime in your PCL
            string? modifiedUtc = post.ModifiedGmt == default
                ? null
                : DateTime.SpecifyKind(post.ModifiedGmt, DateTimeKind.Utc).ToString("o");

            return new Editor.Abstractions.PostDetail(
                Id: post.Id,
                Title: post.Title?.Rendered ?? post.Title?.Raw ?? "",
                Html: raw,
                Status: post.Status.ToString().ToLowerInvariant(),
                CategoryIds: cats,
                ModifiedUtc: modifiedUtc,
                Link: post.Link
            );

        }

        // --------------
        // Autosave (server-side revision)
        // --------------
        public async Task<Editor.Abstractions.SaveResult> AutosaveAsync(Editor.Abstractions.PostDetail post, CancellationToken ct = default)
        {
            if (post.Id <= 0) return await SaveDraftAsync(post, ct); // if new, create draft first

            _ = await _api.GetClientAsync().ConfigureAwait(false);
            var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient not initialized.");

            var payload = new
            {
                title = post.Title,
                content = post.Html,
                categories = post.CategoryIds
            };

            using var res = await http.PostAsJsonAsync($"/wp-json/wp/v2/posts/{post.Id}/autosaves", payload, ct).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();

            DateTimeOffset? serverMod = TryReadDate(body, "modified_gmt");
            return new Editor.Abstractions.SaveResult(Editor.Abstractions.SaveOutcome.Updated, post.Id, post.Status, serverMod, "Autosaved");
        }

        // ------------------
        // Save / Submit / Publish (with conflict checks)
        // ------------------
        public async Task<Editor.Abstractions.SaveResult> SaveDraftAsync(Editor.Abstractions.PostDetail post, CancellationToken ct = default)
            => await SaveWithStatusAsync(post, targetStatus: "draft", ct);

        public async Task<Editor.Abstractions.SaveResult> SubmitForReviewAsync(Editor.Abstractions.PostDetail post, CancellationToken ct = default)
            => await SaveWithStatusAsync(post, targetStatus: "pending", ct);

        public async Task<Editor.Abstractions.SaveResult> PublishAsync(Editor.Abstractions.PostDetail post, CancellationToken ct = default)
            => await SaveWithStatusAsync(post, targetStatus: "publish", ct);

        public async Task<Editor.Abstractions.SaveResult> SwitchToDraftAsync(long id, CancellationToken ct = default)
        {
            var r = await _postEditor.SetStatusAsync(id, "draft", ct).ConfigureAwait(false);
            return new Editor.Abstractions.SaveResult(Editor.Abstractions.SaveOutcome.Updated, id, r.Status, DateTimeOffset.UtcNow, "Switched to draft");
        }

        // -----------
        // Locking
        // -----------
        public async Task<Editor.Abstractions.LockResult> AcquireLockAsync(long postId, CancellationToken ct = default)
        {
            try
            {
                _ = await _api.GetClientAsync().ConfigureAwait(false);
                var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient not initialized.");

                // Read any existing lock first
                var check = await http.GetAsync($"/wp-json/wp/v2/posts/{postId}?context=edit&_fields=meta._edit_lock", ct).ConfigureAwait(false);
                if (check.IsSuccessStatusCode)
                {
                    var rawJson = await check.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    var (lockedBy, _) = ParseLock(rawJson);
                    if (lockedBy is not null)
                    {
                        // See who we are
                        var me = await _api.GetCurrentUserAsync(ct).ConfigureAwait(false);
                        if (lockedBy != me.Id)
                            return new Editor.Abstractions.LockResult(Editor.Abstractions.LockState.AlreadyLocked, lockedBy, $"Locked by {lockedBy}");
                    }
                }

                // Claim: start heartbeat (default interval from options)
                var me2 = await _api.GetCurrentUserAsync(ct).ConfigureAwait(false);
                _lockSession = await _locks.OpenAsync("posts", postId, me2.Id, options: null, ct).ConfigureAwait(false);
                _lockedPostId = postId;
                return new Editor.Abstractions.LockResult(Editor.Abstractions.LockState.Acquired, null, "Lock acquired");
            }
            catch (Exception ex)
            {
                return new Editor.Abstractions.LockResult(Editor.Abstractions.LockState.Failed, null, ex.Message);
            }
        }

        public async Task<Editor.Abstractions.LockResult> ReleaseLockAsync(CancellationToken ct = default)
        {
            try
            {
                if (_lockSession is null)
                    return new Editor.Abstractions.LockResult(Editor.Abstractions.LockState.NotHeld);

                await _lockSession.ReleaseNowAsync(ct).ConfigureAwait(false);
                await _lockSession.DisposeAsync().ConfigureAwait(false);
                _lockSession = null; _lockedPostId = null;
                return new Editor.Abstractions.LockResult(Editor.Abstractions.LockState.Released);
            }
            catch (Exception ex)
            {
                return new Editor.Abstractions.LockResult(Editor.Abstractions.LockState.Failed, null, ex.Message);
            }
        }

        // ---------------
        // Categories
        // ---------------
        public async Task<IReadOnlyList<Editor.Abstractions.CategoryInfo>> ListCategoriesAsync(
            string? search = null, int page = 1, int perPage = 100, int? parentId = null, CancellationToken ct = default)
        {
            _ = await _api.GetClientAsync().ConfigureAwait(false);
            var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient not initialized.");

            var qs = new StringBuilder($"wp/v2/categories?per_page={perPage}&page={page}&orderby=name&order=asc&_fields=id,name,parent");
            if (!string.IsNullOrWhiteSpace(search)) qs.Append("&search=").Append(Uri.EscapeDataString(search));
            if (parentId is not null) qs.Append("&parent=").Append(parentId.Value);

            using var res = await http.GetAsync(qs.ToString(), ct).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();

            var list = new List<Editor.Abstractions.CategoryInfo>();
            using var doc = JsonDocument.Parse(body);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var id = el.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
                var name = el.TryGetProperty("name", out var nEl) ? (nEl.GetString() ?? "") : "";
                int? par = el.TryGetProperty("parent", out var pEl) ? pEl.GetInt32() : 0;
                if (par == 0) par = null;
                list.Add(new Editor.Abstractions.CategoryInfo(id, name, par));
            }
            return list;
        }

        // -----------------------
        // Internal save pipeline
        // -----------------------
        private async Task<Editor.Abstractions.SaveResult> SaveWithStatusAsync(Editor.Abstractions.PostDetail post, string targetStatus, CancellationToken ct)
        {
            try
            {
                if (post.Id <= 0)
                {
                    // Create new draft in one go (title/content/categories)
                    _ = await _api.GetClientAsync().ConfigureAwait(false);
                    var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient not initialized.");

                    var payload = new
                    {
                        title = post.Title,
                        content = post.Html,
                        status = "draft",
                        categories = post.CategoryIds
                    };
                    using var res = await http.PostAsJsonAsync("/wp-json/wp/v2/posts", payload, ct).ConfigureAwait(false);
                    var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    res.EnsureSuccessStatusCode();

                    var newId = TryReadId(body) ?? 0;
                    // Update status if needed
                    if (!EqualsIgnoreCase(targetStatus, "draft"))
                        _ = await _postEditor.SetStatusAsync(newId, targetStatus, ct).ConfigureAwait(false);

                    var outcome = targetStatus switch
                    {
                        "publish" => Editor.Abstractions.SaveOutcome.Published,
                        "pending" => Editor.Abstractions.SaveOutcome.Submitted,
                        _ => Editor.Abstractions.SaveOutcome.Created
                    };
                    var serverMod = TryReadDate(body, "modified_gmt");
                    return new Editor.Abstractions.SaveResult(outcome, newId, targetStatus, serverMod, "Created");
                }
                else
                {
                    // Preflight conflict check against last-seen modified
                    var lastSeen = post.ModifiedUtc ?? await _postEditor.GetLastModifiedUtcAsync(post.Id, ct).ConfigureAwait(false) ?? "";
                    var currentServer = await _postEditor.GetLastModifiedUtcAsync(post.Id, ct).ConfigureAwait(false);
                    var conflict = !string.IsNullOrWhiteSpace(currentServer) &&
                                   !string.Equals(currentServer, lastSeen, Ordinal);

                    // Update content (WordPressEditor handles LWW meta on conflict)
                    var edit = await _postEditor.UpdateAsync(post.Id, post.Html, lastSeen, ct).ConfigureAwait(false);

                    // Also update title/categories if changed (single small PATCH)
                    _ = await PatchTitleAndCategoriesAsync(post.Id, post.Title, post.CategoryIds, ct).ConfigureAwait(false);

                    // Status transition if needed
                    if (!EqualsIgnoreCase(targetStatus, post.Status))
                        edit = await _postEditor.SetStatusAsync(post.Id, targetStatus, ct).ConfigureAwait(false);

                    var serverModAfter = await _postEditor.GetLastModifiedUtcAsync(post.Id, ct).ConfigureAwait(false);
                    var serverModDto = TryParseDate(serverModAfter);
                    var outcome = conflict
                        ? Editor.Abstractions.SaveOutcome.Conflict
                        : targetStatus switch
                        {
                            "publish" => Editor.Abstractions.SaveOutcome.Published,
                            "pending" => Editor.Abstractions.SaveOutcome.Submitted,
                            _ => Editor.Abstractions.SaveOutcome.Updated
                        };

                    // Recovered: WordPressEditor might create a duplicate if original vanished
                    if (edit.Id != post.Id) outcome = Editor.Abstractions.SaveOutcome.Recovered;

                    return new Editor.Abstractions.SaveResult(outcome, edit.Id, targetStatus, serverModDto,
                        outcome == Editor.Abstractions.SaveOutcome.Conflict ? "Saved, but a newer version existed (conflict)." : "Saved");
                }
            }
            catch (Exception ex)
            {
                return new Editor.Abstractions.SaveResult(Editor.Abstractions.SaveOutcome.Error, post.Id, post.Status, DateTimeOffset.UtcNow, ex.Message);
            }
        }

        // -----------------------
        // Helpers
        // -----------------------
        private static bool EqualsIgnoreCase(string a, string b) => string.Equals(a, b, OrdinalIgnoreCase);

        private static int? TryGetIntHeader(HttpResponseMessage res, string name)
            => res.Headers.TryGetValues(name, out var vals) && int.TryParse(vals.FirstOrDefault(), out var n) ? n : null;

        private static DateTimeOffset? TryReadDate(string json, string prop)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty(prop, out var el))
                {
                    var s = el.GetString();
                    return TryParseDate(s);
                }
            }
            catch { }
            return null;
        }

        private static DateTimeOffset? TryParseDate(string? s)
            => DateTimeOffset.TryParse(s, out var dto) ? dto : null;

        private static long? TryReadId(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                return doc.RootElement.TryGetProperty("id", out var el) ? el.GetInt64() : null;
            }
            catch { return null; }
        }

        private static (long? userId, long? ts) ParseLock(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("meta", out var meta) &&
                    meta.TryGetProperty("_edit_lock", out var le))
                {
                    var raw = le.GetString();
                    if (!string.IsNullOrWhiteSpace(raw) && raw.Contains(':'))
                    {
                        var parts = raw.Split(':');
                        var ts = long.TryParse(parts[0], out var t) ? t : default(long?);
                        var uid = long.TryParse(parts[1], out var u) ? u : default(long?);
                        return (uid, ts);
                    }
                }
            }
            catch { }
            return (null, null);
        }

        private async Task<bool> PatchTitleAndCategoriesAsync(long id, string title, IReadOnlyCollection<int> cats, CancellationToken ct)
        {
            _ = await _api.GetClientAsync().ConfigureAwait(false);
            var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient not initialized.");

            var payload = new { title, categories = cats };
            using var res = await http.PostAsJsonAsync($"/wp-json/wp/v2/posts/{id}", payload, ct).ConfigureAwait(false);
            return res.IsSuccessStatusCode;
        }
    }
}
