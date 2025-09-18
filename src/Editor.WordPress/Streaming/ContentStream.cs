using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Editor.Abstractions;

namespace Editor.WordPress;

public sealed class ContentStream : IContentStream
{
    private readonly IWordPressApiService _api;
    private readonly IPostCache _cache;
    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web);

    public ContentStream(IWordPressApiService api, IPostCache cache)
    {
        _api = api;
        _cache = cache;
    }

    public async IAsyncEnumerable<IReadOnlyList<PostSummary>> StreamAllCachedThenFreshAsync(
        string restBase,
        StreamOptions? options = null,
        IProgress<StreamProgress>? progress = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        options ??= new StreamOptions();

        // Ensure the WP client & HttpClient are initialized (and endpoint set)
        _ = await _api.GetClientAsync().ConfigureAwait(false);
        var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

        // Warm pass: include drafts/trashed
        var warmUrl = $"/wp-json/wp/v2/{restBase}?context=edit&status=any&per_page={options.WarmFirstCount}&orderby=modified&order=desc";
        var warmItems = await FetchPageAsync(http, restBase, warmUrl, 1, ct).ConfigureAwait(false);
        {
            var cp = new CachePage(1, warmItems, null, null, DateTimeOffset.UtcNow);
            await _cache.UpsertPageAsync(restBase, cp, ct).ConfigureAwait(false);
            await _cache.UpsertIndexAsync(restBase, warmItems, ct).ConfigureAwait(false);
            yield return warmItems;
            progress?.Report(new StreamProgress(1, 0));
        }

        // Bulk crawl
        int page = 1;
        while (true)
        {
            ct.ThrowIfCancellationRequested();
            var bulkUrl = $"/wp-json/wp/v2/{restBase}?context=edit&status=any&per_page={options.MaxBatchSize}&orderby=modified&order=desc&page={page}";
            var bulkItems = await FetchPageAsync(http, restBase, bulkUrl, page, ct).ConfigureAwait(false);
            if (bulkItems.Count == 0) break;

            var cp = new CachePage(page, bulkItems, null, null, DateTimeOffset.UtcNow);
            await _cache.UpsertPageAsync(restBase, cp, ct).ConfigureAwait(false);
            await _cache.UpsertIndexAsync(restBase, bulkItems, ct).ConfigureAwait(false);
            yield return bulkItems;
            progress?.Report(new StreamProgress(page, 0));
            page++;
            if (bulkItems.Count < options.MaxBatchSize) break;
        }

        // TODO: reconcile deletions with cached index
    }

    private static async Task<IReadOnlyList<PostSummary>> FetchPageAsync(
        HttpClient http, string restBase, string url, int page, CancellationToken ct)
    {
        using var res = await http.GetAsync(url, ct).ConfigureAwait(false);
        if (res.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            return Array.Empty<PostSummary>();
        res.EnsureSuccessStatusCode();
        var arr = await res.Content.ReadFromJsonAsync<JsonElement[]>(JsonOpts, ct).ConfigureAwait(false)
                  ?? Array.Empty<JsonElement>();
        return arr.Select(el =>
        {
            long id = el.GetProperty("id").GetInt64();
            string title = el.GetProperty("title").GetProperty("rendered").GetString() ?? "";
            string status = el.TryGetProperty("status", out var s) ? s.GetString() ?? "" : "";
            string link = el.TryGetProperty("link", out var l) ? l.GetString() ?? "" : "";
            string modified = el.TryGetProperty("modified_gmt", out var mg) ? mg.GetString() ?? "" : "";
            return new PostSummary(id, title, status, link, modified);
        }).ToList();
    }
}
