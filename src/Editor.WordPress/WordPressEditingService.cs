// Path: WordPressEditingService.cs
// Purpose: Thin wrapper over ONLY the custom endpoints (3–5) and defers 1–2 to your existing WordPressApiService
// Strategy implemented:
//  3) Fork Post   → POST /wp-json/rex/v1/fork
//  4) Save Post   → POST /wp-json/rex/v1/save (optimistic concurrency via expected_modified_gmt; auto‑fork on failure)
//  5) Publish     → POST /wp-json/rex/v1/publish (overwrite original if _rex_original_post_id present)
//
// Notes
//  • No raw HttpClient here—this uses IWordPressApiService you already have.
//  • Keep using your WordPressApiService for List/New (core /wp/v2) operations.
//  • DTOs use System.Text.Json with snake_case via [JsonPropertyName].

using System.Text.Json.Serialization;

namespace WpEditing;

public interface IWordPressEditingService
{
    Task<ForkResponse>    ForkAsync(int sourceId, string status = "draft", CancellationToken ct = default);
    Task<SaveResponse>    SaveAsync(int id, SaveData data, CancellationToken ct = default);
    Task<PublishResponse> PublishAsync(int stagingId, CancellationToken ct = default);
}

/// <summary>
/// Minimal adapter that calls custom REX endpoints through your existing IWordPressApiService.
/// </summary>
public sealed class WordPressEditingService : IWordPressEditingService
{
    private readonly IWordPressApiService _wp;

    public WordPressEditingService(IWordPressApiService wp)
        => _wp = wp ?? throw new ArgumentNullException(nameof(wp));

    // 3) Fork Post
    public async Task<ForkResponse> ForkAsync(int sourceId, string status = "draft", CancellationToken ct = default)
    {
        var body = new { source_id = sourceId, status };
        var res  = await _wp.PostJsonAsync<ForkResponse>("wp-json/rex/v1/fork", body, ct);
        return res ?? throw new InvalidOperationException("Fork returned no content");
    }

    // 4) Save Post (may fork on server depending on errors/conflicts)
    public async Task<SaveResponse> SaveAsync(int id, SaveData data, CancellationToken ct = default)
    {
        if (data is null) throw new ArgumentNullException(nameof(data));

        // Guard: never let client send the original marker (server ignores anyway)
        if (data.Meta is not null && data.Meta.ContainsKey("_rex_original_post_id"))
        {
            data = data with { Meta = data.Meta.Where(kv => kv.Key != "_rex_original_post_id").ToDictionary(kv => kv.Key, kv => kv.Value) };
        }

        var body = new SaveRequest { Id = id, Data = data };
        var res  = await _wp.PostJsonAsync<SaveResponse>("wp-json/rex/v1/save", body, ct);
        return res ?? throw new InvalidOperationException("Save returned no content");
    }

    // 5) Publish Post
    public async Task<PublishResponse> PublishAsync(int stagingId, CancellationToken ct = default)
    {
        var body = new { staging_id = stagingId };
        var res  = await _wp.PostJsonAsync<PublishResponse>("wp-json/rex/v1/publish", body, ct);
        return res ?? throw new InvalidOperationException("Publish returned no content");
    }
}

// ===== Adapter contract to your existing HTTP abstraction =====
// If you already have this interface, delete/ignore this and reference your real one.
public interface IWordPressApiService
{
    /// <summary>
    /// Sends a JSON POST to a WP-relative path (e.g., "wp-json/rex/v1/fork").
    /// Must throw on non-2xx; returns deserialized body on success.
    /// </summary>
    Task<T?> PostJsonAsync<T>(string path, object body, CancellationToken ct = default);
}

// ===== Request/Response DTOs =====

public sealed class ForkResponse
{
    [JsonPropertyName("id")]               public int    Id              { get; init; }
    [JsonPropertyName("status")]           public string? Status         { get; init; }
    [JsonPropertyName("original_post_id")] public int?   OriginalPostId  { get; init; }
}

public sealed class SaveRequest
{
    [JsonPropertyName("id")]   public int      Id   { get; init; }
    [JsonPropertyName("data")] public SaveData Data { get; init; } = default!;
}

public sealed record SaveData
(
    [property: JsonPropertyName("post_title")]           string? Title,
    [property: JsonPropertyName("post_content")]         string? Content,
    [property: JsonPropertyName("post_excerpt")]         string? Excerpt,
    [property: JsonPropertyName("post_status")]          string? Status,
    [property: JsonPropertyName("post_name")]            string? Slug,
    [property: JsonPropertyName("meta")]                 Dictionary<string, object>? Meta,
    [property: JsonPropertyName("tax_input")]            Dictionary<string, IEnumerable<int>>? TaxInput,
    [property: JsonPropertyName("expected_modified_gmt")] string? ExpectedModifiedGmt
)
{
    // Convenient builders
    public static SaveData TitleOnly(string title) => new(title, null, null, null, null, null, null, null);
    public SaveData WithContent(string content)     => this with { Content = content };
}

public sealed class SaveResponse
{
    [JsonPropertyName("id")]               public int    Id              { get; init; }
    [JsonPropertyName("status")]           public string? Status         { get; init; }
    [JsonPropertyName("saved")]            public bool?  Saved           { get; init; }
    [JsonPropertyName("forked")]           public bool?  Forked          { get; init; }
    [JsonPropertyName("reason")]           public string? Reason         { get; init; }
    [JsonPropertyName("original_post_id")] public int?   OriginalPostId  { get; init; }
    [JsonPropertyName("modified_gmt")]     public string? ModifiedGmt    { get; init; }
}

public sealed class PublishResponse
{
    [JsonPropertyName("published_id")] public int  PublishedId  { get; init; }
    [JsonPropertyName("used_original")] public bool UsedOriginal { get; init; }
}
