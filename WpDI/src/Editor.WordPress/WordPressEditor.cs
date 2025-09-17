using System.Net;
using System.Text;
using System.Text.Json;
using Editor.Abstractions;

namespace Editor.WordPress;

public sealed class WordPressEditor : IPostEditor
{
    private readonly IWordPressApiService _api;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public WordPressEditor(IWordPressApiService api) => _api = api;

    public async Task<EditResult> CreateAsync(string title, string html, CancellationToken ct = default)
    {
        // ensure API is initialized and get the HttpClient
        _ = await _api.GetClientAsync().ConfigureAwait(false);
        var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

        var payload = new { title, status = "draft", content = html };
        using var res = await http.PostAsync(
            "/wp-json/wp/v2/posts",
            new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json"),
            ct).ConfigureAwait(false);
        return await ParseOrThrow(res, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Update a post with Last-Write-Wins semantics; emits conflict meta if server modified diverges.
    /// </summary>
    public async Task<EditResult> UpdateAsync(long id, string html, string lastSeenModifiedUtc, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(lastSeenModifiedUtc))
            throw new ArgumentException("lastSeenModifiedUtc is required (ISO-8601 or WP modified_gmt).", nameof(lastSeenModifiedUtc));

        _ = await _api.GetClientAsync().ConfigureAwait(false);
        var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

        // Preflight (bypass caches)
        using var preReq = new HttpRequestMessage(HttpMethod.Get, $"/wp-json/wp/v2/posts/{id}?context=edit&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        preReq.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        preReq.Headers.Pragma.ParseAdd("no-cache");

        using var pre = await http.SendAsync(preReq, ct).ConfigureAwait(false);

        if (pre.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
            // Create a duplicate draft with typed meta
            var reason = pre.StatusCode == HttpStatusCode.NotFound ? ReasonCode.NotFound : ReasonCode.Trashed;
            var meta = new
            {
                kind = "duplicate",
                reason = new { code = reason.ToString(), args = new { kind = "post", id } },
                originalId = id,
                timestampUtc = DateTime.UtcNow.ToString("o")
            };
            var duplicateTitle = $"Recovered #{id} {DateTime.UtcNow:yyyy-MM-dd HH:mm} UTC";
            var payload = new { title = duplicateTitle, status = "draft", content = html, meta = new { wpdi_info = meta } };

            using var resDup = await http.PostAsync(
                "/wp-json/wp/v2/posts",
                new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json"),
                ct).ConfigureAwait(false);
            return await ParseOrThrow(resDup, ct).ConfigureAwait(false);
        }

        // Otherwise: attempt normal update in place (and detect divergence for LWW warning)
        string? serverModifiedUtc = null;
        if (pre.IsSuccessStatusCode)
        {
            var preBody = await pre.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            try
            {
                using var doc = JsonDocument.Parse(preBody);
                if (doc.RootElement.TryGetProperty("modified_gmt", out var mg))
                    serverModifiedUtc = mg.GetString();
            }
            catch
            {
                // If parsing fails, we won't emit conflict meta (still proceed with LWW).
            }
        }

        var conflict = !string.IsNullOrWhiteSpace(serverModifiedUtc) &&
                       !string.Equals(serverModifiedUtc, lastSeenModifiedUtc, StringComparison.Ordinal);

        object updPayload = conflict
            ? new
            {
                content = html,
                meta = new
                {
                    wpdi_info = new
                    {
                        kind = "warning",
                        reason = new { code = ReasonCode.Conflict.ToString(), args = new { kind = "post", id } },
                        baseModifiedUtc = lastSeenModifiedUtc,
                        serverModifiedUtc,
                        timestampUtc = DateTime.UtcNow.ToString("o")
                    }
                }
            }
            : new { content = html };

        using var res = await http.PostAsync(
            $"/wp-json/wp/v2/posts/{id}",
            new StringContent(JsonSerializer.Serialize(updPayload, Json), Encoding.UTF8, "application/json"),
            ct).ConfigureAwait(false);
        return await ParseOrThrow(res, ct).ConfigureAwait(false);
    }

    private static async Task<EditResult> ParseOrThrow(HttpResponseMessage res, CancellationToken ct)
    {
        var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode) throw new WordPressApiException(res.StatusCode, body);
        using var doc = JsonDocument.Parse(body);
        var r = doc.RootElement;
        return new EditResult(
            r.GetProperty("id").GetInt64(),
            r.GetProperty("link").GetString()!,
            r.GetProperty("status").GetString()!
        );
    }
}
