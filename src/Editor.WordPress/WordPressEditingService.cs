// Path: src/Editor.WordPress/WordPressEditingService.cs
// Purpose: Defines the IWordPressEditingService contract and related DTOs for custom REX endpoints.

using System.Text.Json.Serialization;

namespace WpEditing;

public interface IWordPressEditingService
{
    Task<ForkResponse>    ForkAsync(int sourceId, string status = "draft", CancellationToken ct = default);
    Task<SaveResponse>    SaveAsync(int id, SaveData data, CancellationToken ct = default);
    Task<PublishResponse> PublishAsync(int stagingId, CancellationToken ct = default);
}

// ===== Request/Response DTOs =====

public sealed class ForkResponse
{
    [JsonPropertyName("id")]               public int    Id              { get; init; }
    [JsonPropertyName("status")]           public string? Status         { get; init; }
    [JsonPropertyName("original_post_id")] public int?   OriginalPostId  { get; init; }
}

public sealed record SaveData
(
    [property: JsonPropertyName("post_title")]            string? Title,
    [property: JsonPropertyName("post_content")]          string? Content,
    [property: JsonPropertyName("post_excerpt")]          string? Excerpt,
    [property: JsonPropertyName("post_status")]           string? Status,
    [property: JsonPropertyName("post_name")]             string? Slug,
    [property: JsonPropertyName("meta")]                  Dictionary<string, object>? Meta,
    [property: JsonPropertyName("tax_input")]             Dictionary<string, IEnumerable<int>>? TaxInput,
    [property: JsonPropertyName("expected_modified_gmt")] string? ExpectedModifiedGmt
)
{
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
