// WpDI/tests/Editor.Tests/Streaming/StreamingTestHelpers.cs
using System.Net.Http.Json;
using System.Text.Json;
using Editor.Abstractions;

internal static class StreamingTestHelpers
{
    public static async Task<long> CreatePostAsync(HttpClient http, string title, string html, string status = "draft")
    {
        var res = await http.PostAsJsonAsync("/wp-json/wp/v2/posts", new { title, status, content = html });
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetInt64();
    }

    public static async Task DeletePostHardAsync(HttpClient http, long id)
    {
        var res = await http.DeleteAsync($"/wp-json/wp/v2/posts/{id}?force=true");
        if (!res.IsSuccessStatusCode && (int)res.StatusCode != 404) res.EnsureSuccessStatusCode();
    }

    public static async Task<long> CreateOfficeAsync(HttpClient http, string restBase, string title, object data, string status = "draft")
    {
        var res = await http.PostAsJsonAsync($"/wp-json/wp/v2/{restBase}", new { title, status, data });
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetInt64();
    }

    public static async Task DeleteOfficeHardAsync(HttpClient http, string restBase, long id)
    {
        var res = await http.DeleteAsync($"/wp-json/wp/v2/{restBase}/{id}?force=true");
        if (!res.IsSuccessStatusCode && (int)res.StatusCode != 404) res.EnsureSuccessStatusCode();
    }

    public static async Task<IReadOnlyList<PostSummary>> NextSnapshotOrTimeoutAsync(
        IAsyncEnumerable<IReadOnlyList<PostSummary>> stream,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        await foreach (var snap in stream.WithCancellation(cts.Token))
            return snap;
        throw new TimeoutException("No snapshot within timeout.");
    }
}
