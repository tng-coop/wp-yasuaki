using System.Text;
using System.Text.Json;
using Editor.Abstractions;

namespace Editor.WordPress;

public sealed class WordPressEditor : IPostEditor
{
    private readonly HttpClient _http;
    private static readonly JsonSerializerOptions Json = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };
    public WordPressEditor(HttpClient http) => _http = http;

    public async Task<EditResult> CreateAsync(string title, string html, CancellationToken ct = default)
    {
        var payload = new { title, status = "draft", content = html };
        using var res = await _http.PostAsync("/wp-json/wp/v2/posts",
            new StringContent(System.Text.Json.JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json"), ct);
        return await ParseOrThrow(res, ct);
    }

    public async Task<EditResult> UpdateAsync(long id, string html, CancellationToken ct = default)
    {
        var payload = new { content = html };
        using var res = await _http.PostAsync($"/wp-json/wp/v2/posts/{id}",
            new StringContent(System.Text.Json.JsonSerializer.Serialize(payload, Json), Encoding.UTF8, "application/json"), ct);
        return await ParseOrThrow(res, ct);
    }

    private static async Task<EditResult> ParseOrThrow(HttpResponseMessage res, CancellationToken ct)
    {
        var body = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode) throw new WordPressApiException(res.StatusCode, body);
        using var doc = JsonDocument.Parse(body);
        var r = doc.RootElement;
        return new EditResult(r.GetProperty("id").GetInt64(), r.GetProperty("link").GetString()!, r.GetProperty("status").GetString()!);
    }
}
