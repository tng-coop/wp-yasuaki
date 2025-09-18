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

        // -------- Preflight: read server-modified (bypass caches) --------
        using var preReq = new HttpRequestMessage(
            HttpMethod.Get,
            $"/wp-json/wp/v2/posts/{id}?context=edit&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"
        );
        preReq.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        preReq.Headers.Pragma.ParseAdd("no-cache");

        using var pre = await http.SendAsync(preReq, ct).ConfigureAwait(false);

        // If missing/trashed → create a duplicate draft with wpdi_info meta
        if (pre.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
        {
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

        // Otherwise: normal update; detect divergence for LWW warning
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
                // ignore parse errors; proceed without conflict meta
            }
        }

        var conflict =
            !string.IsNullOrWhiteSpace(serverModifiedUtc) &&
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

    // ---------------------- NEW METHODS (Option B) ----------------------

    /// <summary>
    /// Read server-side modified time (modified_gmt) for LWW preflight.
    /// Returns null if unavailable or non-200.
    /// </summary>
    public async Task<string?> GetLastModifiedUtcAsync(long id, CancellationToken ct = default)
    {
        _ = await _api.GetClientAsync().ConfigureAwait(false);
        var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

        using var req = new HttpRequestMessage(
            HttpMethod.Get,
            $"/wp-json/wp/v2/posts/{id}?context=edit&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}"
        );
        req.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        req.Headers.Pragma.ParseAdd("no-cache");

        using var res = await http.SendAsync(req, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode) return null;

        var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        try
        {
            using var doc = JsonDocument.Parse(body);
            return doc.RootElement.TryGetProperty("modified_gmt", out var mg) ? mg.GetString() : null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Set status (draft/pending/publish…); returns EditResult parsed from REST response.
    /// </summary>
    public async Task<EditResult> SetStatusAsync(long id, string status, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(status))
            throw new ArgumentException("status is required", nameof(status));

        _ = await _api.GetClientAsync().ConfigureAwait(false);
        var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

        var payload = new { status };
        using var res = await http.PostAsync(
            $"/wp-json/wp/v2/posts/{id}",
            new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json"),
            ct).ConfigureAwait(false);

        return await ParseOrThrow(res, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Delete a post. Accepts 200/404/410 as success (already gone).
    /// </summary>
    public async Task DeleteAsync(long id, bool force = true, CancellationToken ct = default)
    {
        _ = await _api.GetClientAsync().ConfigureAwait(false);
        var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

        var url = $"/wp-json/wp/v2/posts/{id}?force={force.ToString().ToLowerInvariant()}";
        using var res = await http.DeleteAsync(url, ct).ConfigureAwait(false);

        if (!res.IsSuccessStatusCode &&
            res.StatusCode is not HttpStatusCode.NotFound and not HttpStatusCode.Gone)
        {
            res.EnsureSuccessStatusCode();
        }
        // otherwise: treat as success
    }

    // -------------------------------------------------------------------

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
