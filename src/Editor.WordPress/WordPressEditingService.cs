// src/Editor.WordPress/WordPressEditingService.cs
#nullable enable
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Collections.Concurrent;
using Editor.Abstractions;           // PostSummary, IPostCache, etc.
using WordPressPCL;

namespace Editor.WordPress
{
    // -------------------------------
    // Intent-level UI façade types
    // -------------------------------

    public record Paged<T>(IReadOnlyList<T> Items, int Page, int PerPage, int? Total, int? TotalPages);

    public record PostDetail(
        long Id,
        string Title,
        string Html,
        string Status,
        IReadOnlyList<int> CategoryIds,
        string? ModifiedUtc,
        string? Link
    );

    public enum SaveOutcome { Created, Updated, Submitted, Published, Recovered, Conflict, AuthRequired, RateLimited, Error }

    public record SaveResult(
        SaveOutcome Outcome,
        long PostId,
        string Status,
        DateTimeOffset? ServerModifiedUtc,
        string? Message = null
    );

    public enum LockState { Acquired, AlreadyLocked, Released, NotHeld, Failed }

    public record LockResult(LockState State, long? OtherUserId = null, string? Message = null);

    public record CategoryInfo(int Id, string Name, int? ParentId);

    public interface IEditingService
    {
        // List (paged)
        Task<Paged<PostSummary>> ListPostsAsync(
            int page = 1, int perPage = 20,
            string? search = null, string? statusCsv = "draft,pending,publish,private",
            int? categoryId = null, CancellationToken ct = default);

        // Read (edit context)
        Task<PostDetail?> GetPostAsync(long id, CancellationToken ct = default);

        // Server autosave (parent post stays unchanged from the UI perspective)
        Task<SaveResult> AutosaveAsync(PostDetail post, CancellationToken ct = default);

        // Save/submit/publish (with conflict checks)
        Task<SaveResult> SaveDraftAsync(PostDetail post, CancellationToken ct = default);
        Task<SaveResult> SubmitForReviewAsync(PostDetail post, CancellationToken ct = default);
        Task<SaveResult> PublishAsync(PostDetail post, CancellationToken ct = default);

        // Unpublish
        Task<SaveResult> SwitchToDraftAsync(long id, CancellationToken ct = default);

        // Locking
        Task<LockResult> AcquireLockAsync(long postId, CancellationToken ct = default);
        Task<LockResult> ReleaseLockAsync(CancellationToken ct = default);

        // Categories
        Task<IReadOnlyList<CategoryInfo>> ListCategoriesAsync(
            string? search = null, int page = 1, int perPage = 100, int? parentId = null, CancellationToken ct = default);
    }

    // --------------------------------------
    // Implementation over WP REST + PCL
    // --------------------------------------
    public sealed class WordPressEditingService : IEditingService
    {
        private readonly IWordPressApiService _api;
        private readonly IPostEditor _postEditor;
        private readonly IEditLockService _locks;

        private IEditLockSession? _lockSession;
        private long? _lockedPostId;

        private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web);

        // Service-owned concurrency token: parent post modified_gmt (ISO-8601 UTC) per post id.
        private readonly ConcurrentDictionary<long, string?> _lastSeenModifiedUtc = new();

        public WordPressEditingService(IWordPressApiService api, IPostEditor postEditor, IEditLockService locks)
        {
            _api = api ?? throw new ArgumentNullException(nameof(api));
            _postEditor = postEditor ?? throw new ArgumentNullException(nameof(postEditor));
            _locks = locks ?? throw new InvalidOperationException(nameof(locks));
        }

        // -------------------
        // Listing / Reading
        // -------------------
        public async Task<Paged<PostSummary>> ListPostsAsync(
            int page = 1, int perPage = 20,
            string? search = null, string? statusCsv = "draft,pending,publish,private",
            int? categoryId = null, CancellationToken ct = default)
        {
            _ = await _api.GetClientAsync().ConfigureAwait(false);
            var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient not initialized.");

            // BaseAddress is expected to be .../wp-json/
            var qs = new StringBuilder($"wp/v2/posts?context=edit&_embed=1&per_page={perPage}&page={page}");
            if (!string.IsNullOrWhiteSpace(search)) qs.Append("&search=").Append(Uri.EscapeDataString(search));
            if (!string.IsNullOrWhiteSpace(statusCsv)) qs.Append("&status=").Append(Uri.EscapeDataString(statusCsv));
            if (categoryId is not null) qs.Append("&categories=").Append(categoryId.Value);

            using var res = await http.GetAsync(qs.ToString(), ct).ConfigureAwait(false);
            var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            res.EnsureSuccessStatusCode();

            var items = new List<PostSummary>();
            using (var doc = JsonDocument.Parse(body))
            {
                foreach (var el in doc.RootElement.EnumerateArray())
                {
                    var id = el.TryGetProperty("id", out var idEl) ? idEl.GetInt64() : 0;
                    var status = el.TryGetProperty("status", out var sEl) ? sEl.GetString() ?? "" : "";
                    var link = el.TryGetProperty("link", out var lEl) ? lEl.GetString() ?? "" : "";
                    var title = el.TryGetProperty("title", out var tEl) && tEl.TryGetProperty("rendered", out var tr)
                                    ? (tr.GetString() ?? "")
                                    : "";
                    var modified = el.TryGetProperty("modified_gmt", out var mg) ? (mg.GetString() ?? "") : "";

                    items.Add(new PostSummary(id, title, status, link, modified));
                }
            }

            int? total = TryGetIntHeader(res, "X-WP-Total");
            int? totalPages = TryGetIntHeader(res, "X-WP-TotalPages");

            return new Paged<PostSummary>(items, page, perPage, total, totalPages);
        }

        public async Task<PostDetail?> GetPostAsync(long id, CancellationToken ct = default)
        {
            var client = await _api.GetClientAsync().ConfigureAwait(false);
            if (client is null) throw new InvalidOperationException("WordPress client not initialized.");

            var post = await client.Posts.GetByIDAsync((int)id, embed: true, useAuth: true).ConfigureAwait(false);
            if (post is null) return null;

            // Use rendered HTML
            var html = post.Content?.Rendered ?? string.Empty;
            var cats = post.Categories ?? new List<int>();

            // ModifiedGmt in the PCL is non-nullable; default means "unknown".
            string? modifiedUtc = post.ModifiedGmt == default
                ? null
                : DateTime.SpecifyKind(post.ModifiedGmt, DateTimeKind.Utc).ToString("o");

            // Seed/refresh token from parent post on read.
            if (!string.IsNullOrWhiteSpace(modifiedUtc))
                _lastSeenModifiedUtc[id] = modifiedUtc;

            return new PostDetail(
                Id: post.Id,
                Title: post.Title?.Rendered ?? string.Empty,
                Html: html,
                Status: post.Status.ToString().ToLowerInvariant(),
                CategoryIds: cats,
                ModifiedUtc: modifiedUtc,
                Link: post.Link
            );
        }

        // --------------
        // Autosave (server-side revision)
        // --------------
        public async Task<SaveResult> AutosaveAsync(PostDetail post, CancellationToken ct = default)
        {
            if (post.Id <= 0) return await SaveDraftAsync(post, ct); // new → create draft once

            _ = await _api.GetClientAsync().ConfigureAwait(false);
            var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient not initialized.");

            var payload = new
            {
                title = post.Title,
                content = post.Html,
                categories = post.CategoryIds
            };

            using var res = await http.PostAsJsonAsync($"wp/v2/posts/{post.Id}/autosaves", payload, ct).ConfigureAwait(false);
            // we don’t need the body string here anymore, just ensure status
            res.EnsureSuccessStatusCode();

            // IMPORTANT: autosave is a revision; keep ServerModifiedUtc null
            // But on some servers autosave *does* bump parent modified_gmt. Refresh token to stay in sync.
            using var reread = await http.GetAsync($"wp/v2/posts/{post.Id}?context=edit&_fields=modified_gmt", ct).ConfigureAwait(false);
            var body2 = await reread.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            reread.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(body2);
            if (doc.RootElement.TryGetProperty("modified_gmt", out var mg) && mg.GetString() is string s && s.Length > 0)
            {
                if (DateTimeOffset.TryParse(s, out var dto))
                    RememberToken(post.Id, dto.ToUniversalTime().ToString("o"));
                else
                    RememberToken(post.Id, s);
            }

            return new SaveResult(SaveOutcome.Updated, post.Id, post.Status, null, "Autosaved");
        }


        // ------------------
        // Save / Submit / Publish (with conflict checks)
        // ------------------
        public Task<SaveResult> SaveDraftAsync(PostDetail post, CancellationToken ct = default)
            => SaveWithStatusAsync(post, targetStatus: "draft", ct);

        public Task<SaveResult> SubmitForReviewAsync(PostDetail post, CancellationToken ct = default)
            => SaveWithStatusAsync(post, targetStatus: "pending", ct);

        public Task<SaveResult> PublishAsync(PostDetail post, CancellationToken ct = default)
            => SaveWithStatusAsync(post, targetStatus: "publish", ct);

        public async Task<SaveResult> SwitchToDraftAsync(long id, CancellationToken ct = default)
        {
            var r = await _postEditor.SetStatusAsync(id, "draft", ct).ConfigureAwait(false);

            // After status change, refresh token from parent post.
            var serverModAfter = await _postEditor.GetLastModifiedUtcAsync(id, ct).ConfigureAwait(false);
            if (DateTimeOffset.TryParse(serverModAfter, out var dtoSw))
                RememberToken(id, dtoSw.ToUniversalTime().ToString("o"));
            else
                RememberToken(id, serverModAfter);

            return new SaveResult(SaveOutcome.Updated, id, r.Status, TryParseDate(serverModAfter), "Switched to draft");
        }

        // -----------
        // Locking
        // -----------
        public async Task<LockResult> AcquireLockAsync(long postId, CancellationToken ct = default)
        {
            try
            {
                _ = await _api.GetClientAsync().ConfigureAwait(false);
                var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient not initialized.");

                // Read any existing lock
                var check = await http.GetAsync($"wp/v2/posts/{postId}?context=edit&_fields=meta._edit_lock", ct).ConfigureAwait(false);
                if (check.IsSuccessStatusCode)
                {
                    var rawJson = await check.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    var (lockedBy, _) = ParseLock(rawJson);
                    if (lockedBy is not null)
                    {
                        var me = await _api.GetCurrentUserAsync(ct).ConfigureAwait(false);
                        if (lockedBy != me.Id)
                            return new LockResult(LockState.AlreadyLocked, lockedBy, $"Locked by {lockedBy}");
                    }
                }

                var me2 = await _api.GetCurrentUserAsync(ct).ConfigureAwait(false);
                _lockSession = await _locks.OpenAsync("posts", postId, me2.Id, options: null, ct).ConfigureAwait(false);
                _lockedPostId = postId;
                return new LockResult(LockState.Acquired, null, "Lock acquired");
            }
            catch (Exception ex)
            {
                return new LockResult(LockState.Failed, null, ex.Message);
            }
        }

        public async Task<LockResult> ReleaseLockAsync(CancellationToken ct = default)
        {
            try
            {
                if (_lockSession is null)
                    return new LockResult(LockState.NotHeld);

                await _lockSession.ReleaseNowAsync(ct).ConfigureAwait(false);
                await _lockSession.DisposeAsync().ConfigureAwait(false);
                _lockSession = null; _lockedPostId = null;
                return new LockResult(LockState.Released);
            }
            catch (Exception ex)
            {
                return new LockResult(LockState.Failed, null, ex.Message);
            }
        }

        // ---------------
        // Categories
        // ---------------
        public async Task<IReadOnlyList<CategoryInfo>> ListCategoriesAsync(
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

            var list = new List<CategoryInfo>();
            using var doc = JsonDocument.Parse(body);
            foreach (var el in doc.RootElement.EnumerateArray())
            {
                var id = el.TryGetProperty("id", out var idEl) ? idEl.GetInt32() : 0;
                var name = el.TryGetProperty("name", out var nEl) ? (nEl.GetString() ?? "") : "";
                int? par = el.TryGetProperty("parent", out var pEl) ? pEl.GetInt32() : 0;
                if (par == 0) par = null;
                list.Add(new CategoryInfo(id, name, par));
            }
            return list;
        }

        // -----------------------
        // Internal save pipeline
        // -----------------------
        private async Task<SaveResult> SaveWithStatusAsync(PostDetail post, string targetStatus, CancellationToken ct)
        {
            try
            {
                if (post.Id <= 0)
                {
                    // Create new draft in one go
                    _ = await _api.GetClientAsync().ConfigureAwait(false);
                    var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient not initialized.");

                    var payload = new
                    {
                        title = post.Title,
                        content = post.Html,
                        status = "draft",
                        categories = post.CategoryIds
                    };
                    using var res = await http.PostAsJsonAsync("wp/v2/posts", payload, ct).ConfigureAwait(false);
                    var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                    res.EnsureSuccessStatusCode();

                    var newId = TryReadId(body) ?? 0;

                    if (!targetStatus.Equals("draft", StringComparison.OrdinalIgnoreCase))
                        _ = await _postEditor.SetStatusAsync(newId, targetStatus, ct).ConfigureAwait(false);

                    var outcome = targetStatus.ToLowerInvariant() switch
                    {
                        "publish" => SaveOutcome.Published,
                        "pending" => SaveOutcome.Submitted,
                        _ => SaveOutcome.Created
                    };

                    // Seed the token from the parent post’s modified_gmt on creation (normalized).
                    var serverMod = TryReadDate(body, "modified_gmt");
                    if (serverMod is not null)
                        RememberToken(newId, serverMod.Value.ToUniversalTime().ToString("o"));

                    return new SaveResult(outcome, newId, targetStatus, serverMod, "Created");
                }
                else
                {
                    // ------- Conflict check using normalized timestamps -------
                    // Priority: service-tracked token → post.ModifiedUtc (initial load) → server read
                    var lastSeenRaw =
                        (_lastSeenModifiedUtc.TryGetValue(post.Id, out var tracked) ? tracked : null)
                        ?? post.ModifiedUtc
                        ?? await _postEditor.GetLastModifiedUtcAsync(post.Id, ct).ConfigureAwait(false)
                        ?? "";

                    var currentRaw = await _postEditor.GetLastModifiedUtcAsync(post.Id, ct).ConfigureAwait(false) ?? "";

                    bool TryParse(string s, out DateTimeOffset dto) => DateTimeOffset.TryParse(s, out dto);
                    bool conflict =
                        TryParse(currentRaw, out var currentDto) && TryParse(lastSeenRaw, out var seenDto)
                            ? currentDto != seenDto
                            : !string.Equals(currentRaw, lastSeenRaw, StringComparison.Ordinal);

                    // Update content via IPostEditor (handles LWW meta + recovery)
                    var edit = await _postEditor.UpdateAsync(post.Id, post.Html, lastSeenRaw, ct).ConfigureAwait(false);

                    // Patch title/categories (single small POST)
                    _ = await PatchTitleAndCategoriesAsync(post.Id, post.Title, post.CategoryIds, ct).ConfigureAwait(false);

                    // Status transition
                    if (!targetStatus.Equals(post.Status, StringComparison.OrdinalIgnoreCase))
                        edit = await _postEditor.SetStatusAsync(post.Id, targetStatus, ct).ConfigureAwait(false);

                    // Refresh token from parent post after a real save
                    var serverModAfter = await _postEditor.GetLastModifiedUtcAsync(post.Id, ct).ConfigureAwait(false);
                    DateTimeOffset? serverDto = TryParseDate(serverModAfter);
                    if (serverDto is not null)
                        RememberToken(edit.Id, serverDto.Value.ToUniversalTime().ToString("o"));
                    else
                        RememberToken(edit.Id, serverModAfter);

                    var outcome = conflict
                        ? SaveOutcome.Conflict
                        : targetStatus.ToLowerInvariant() switch
                        {
                            "publish" => SaveOutcome.Published,
                            "pending" => SaveOutcome.Submitted,
                            _ => SaveOutcome.Updated
                        };

                    if (edit.Id != post.Id) outcome = SaveOutcome.Recovered;

                    return new SaveResult(outcome, edit.Id, targetStatus, serverDto,
                        outcome == SaveOutcome.Conflict ? "Saved, but a newer version existed (conflict)." : "Saved");
                }
            }
            catch (Exception ex)
            {
                return new SaveResult(SaveOutcome.Error, post.Id, post.Status, DateTimeOffset.UtcNow, ex.Message);
            }
        }

        // -----------------------
        // Helpers
        // -----------------------
        private static int? TryGetIntHeader(HttpResponseMessage res, string name)
            => res.Headers.TryGetValues(name, out var vals) && int.TryParse(vals.FirstOrDefault(), out var n) ? n : null;

        private static DateTimeOffset? TryReadDate(string json, string prop)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty(prop, out var el))
                    return TryParseDate(el.GetString());
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
            using var res = await http.PostAsJsonAsync($"wp/v2/posts/{id}", payload, ct).ConfigureAwait(false);
            return res.IsSuccessStatusCode;
        }

        private void RememberToken(long postId, string? modifiedUtc)
        {
            if (!string.IsNullOrWhiteSpace(modifiedUtc))
                _lastSeenModifiedUtc[postId] = modifiedUtc;
        }
    }
}
