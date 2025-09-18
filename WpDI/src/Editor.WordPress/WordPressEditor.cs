// WpDI/src/Editor.WordPress/WordPressEditor.cs
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
        _ = await _api.GetClientAsync().ConfigureAwait(false);
        var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

        var payload = new { title, status = "draft", content = html };
        using var res = await http.PostAsync(
            "/wp-json/wp/v2/posts",
            new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json"),
            ct).ConfigureAwait(false);
        return await ParseOrThrow(res, ct).ConfigureAwait(false);
    }

    public async Task<EditResult> UpdateAsync(long id, string html, string lastSeenModifiedUtc, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(lastSeenModifiedUtc))
            throw new ArgumentException("lastSeenModifiedUtc is required (ISO-8601 or WP modified_gmt).", nameof(lastSeenModifiedUtc));

        _ = await _api.GetClientAsync().ConfigureAwait(false);
        var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

        // (This is your existing logic; unchanged apart from context.)
        // ... keep your preflight & conflict handling ...
        // (omitted here for brevity — it’s already in your file)
        // return await ParseOrThrow(res, ct).ConfigureAwait(false);
        throw new NotImplementedException("Keep your existing UpdateAsync body here.");
    }

    // NEW
    public async Task<string?> GetLastModifiedUtcAsync(long id, CancellationToken ct = default)
    {
        _ = await _api.GetClientAsync().ConfigureAwait(false);
        var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

        using var req = new HttpRequestMessage(HttpMethod.Get, $"/wp-json/wp/v2/posts/{id}?context=edit&_={DateTimeOffset.UtcNow.ToUnixTimeMilliseconds()}");
        req.Headers.CacheControl = new System.Net.Http.Headers.CacheControlHeaderValue { NoCache = true };
        req.Headers.Pragma.ParseAdd("no-cache");

        using var res = await http.SendAsync(req, ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode) return null;

        var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("modified_gmt", out var mg))
                return mg.GetString();
        }
        catch { /* ignore parse errors; return null */ }
        return null;
    }

    // NEW
    public async Task<EditResult> SetStatusAsync(long id, string status, CancellationToken ct = default)
    {
        _ = await _api.GetClientAsync().ConfigureAwait(false);
        var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

        var payload = new { status };
        using var res = await http.PostAsync(
            $"/wp-json/wp/v2/posts/{id}",
            new StringContent(JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json"),
            ct).ConfigureAwait(false);
        return await ParseOrThrow(res, ct).ConfigureAwait(false);
    }

    // NEW
    public async Task DeleteAsync(long id, bool force = true, CancellationToken ct = default)
    {
        _ = await _api.GetClientAsync().ConfigureAwait(false);
        var http = _api.HttpClient ?? throw new InvalidOperationException("WordPress HttpClient is not initialized.");

        using var res = await http.DeleteAsync($"/wp-json/wp/v2/posts/{id}?force={force.ToString().ToLowerInvariant()}", ct)
                                  .ConfigureAwait(false);
        // Treat 404/410 as already-gone; otherwise ensure success
        if (!res.IsSuccessStatusCode &&
            res.StatusCode is not HttpStatusCode.NotFound and not HttpStatusCode.Gone)
        {
            res.EnsureSuccessStatusCode();
        }
    }

    private static async Task<EditResult> ParseOrThrow(HttpResponseMessage res, CancellationToken ct)
    {
        var body = await res.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
        if (!res.IsSuccessStatusCode) throw new WordPressApiException(res.StatusCode, body);
        using var doc = JsonDocument.Parse(body);
        var r = doc.RootElement;
        return new EditResult(
            r.GetProperty("id").GetInt64(),
            r.GetProperty("link").GetString()!,   // permalink
            r.GetProperty("status").GetString()!  // status
        );
    }
}
