// Path: WpEditing.IntegrationTests/WordPressEditingServiceTests.cs
// xUnit integration tests that hit a REAL WordPress server (no mocking)
// Env vars required: WP_BASE_URL, WP_USERNAME, WP_APP_PASSWORD
// Optional: WP_VERIFY_SSL (true/false; default false for .lan)

using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Xunit;

namespace WpEditing.IntegrationTests;

public sealed class WordPressEditingServiceTests : IClassFixture<WpFixture>
{
    private readonly WpFixture _fx;
    private readonly WordPressEditingService _svc;

    public WordPressEditingServiceTests(WpFixture fx)
    {
        _fx = fx;
        _svc = fx.CreateEditingService();
    }

    [Fact]
    public async Task Save_Succeeds_NoFork_With_OptimisticConcurrency()
    {
        var ts = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var draft = await _fx.Core.CreatePostAsync(new CoreCreatePost
        {
            title = $"REX SaveOK {ts}",
            content = "v1",
            status = "draft"
        });

        // First save
        var r1 = await _svc.SaveAsync(draft.id, SaveData.TitleOnly("REX SaveOK A1"));
        Assert.True(r1.Saved is true);
        Assert.Equal(draft.id, r1.Id);

        // Grab concurrency token (fallback to GET if api didn’t return it)
        var token = r1.ModifiedGmt ?? (await _fx.Core.GetPostAsync(draft.id, "modified_gmt")).modified_gmt;
        Assert.False(string.IsNullOrWhiteSpace(token));

        // Second save with token
        var r2 = await _svc.SaveAsync(draft.id, new SaveData(null, "v2 - concurrency pass", null, null, null, null, null, token));
        Assert.True(r2.Saved is true);
        Assert.Equal(draft.id, r2.Id);

        // Verify persisted content
        var post = await _fx.Core.GetPostAsync(draft.id);
        Assert.Contains("REX SaveOK A1", post.title?.rendered ?? string.Empty);
        var text = WebUtility.HtmlDecode(post.content?.rendered ?? string.Empty);
        Assert.Contains("concurrency pass", text, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ForkOfFork_PointsToRoot()
    {
        var orig = await _fx.Core.CreatePostAsync(new CoreCreatePost
        {
            title = "Root",
            content = "root",
            status = "publish"
        });
        var f1 = await _svc.ForkAsync(orig.id);
        Assert.Equal(orig.id, f1.OriginalPostId);

        var f2 = await _svc.ForkAsync(f1.Id);
        Assert.Equal(orig.id, f2.OriginalPostId);
    }

    [Fact]
    public async Task Publish_Untrash_And_Overwrite_Original()
    {
        var orig = await _fx.Core.CreatePostAsync(new CoreCreatePost
        {
            title = "Live",
            content = "C",
            status = "publish"
        });
        var stg = await _svc.ForkAsync(orig.id);

        // Trash original (soft delete)
        await _fx.Core.DeletePostAsync(orig.id, force: false);

        // Update staging and publish
        await _svc.SaveAsync(stg.Id, new SaveData("New Title", "Updated before publish", null, null, null, null, null, null));
        var pub = await _svc.PublishAsync(stg.Id);
        Assert.True(pub.UsedOriginal);
        Assert.Equal(orig.id, pub.PublishedId);

        var after = await _fx.Core.GetPostAsync(orig.id, "id,status,title");
        Assert.Equal("publish", after.status);
        Assert.Contains("New Title", after.title?.rendered ?? string.Empty);
    }

    [Fact]
    public async Task Publish_Fallback_When_Original_HardDeleted_ClearsMarker()
    {
        var orig = await _fx.Core.CreatePostAsync(new CoreCreatePost
        {
            title = "Live",
            content = "C",
            status = "publish"
        });
        var stg = await _svc.ForkAsync(orig.id);

        // Hard delete original
        await _fx.Core.DeletePostAsync(orig.id, force: true);

        var pub = await _svc.PublishAsync(stg.Id);
        Assert.False(pub.UsedOriginal);
        Assert.Equal(stg.Id, pub.PublishedId);

        // Ensure original marker cleared on the published staging
        var after = await _fx.Core.GetPostAsync(stg.Id, "id,status,meta");
        var marker = after.meta?.GetPropertyOrDefault("_rex_original_post_id");
        Assert.True(marker is null || marker.Value.ValueKind == JsonValueKind.Null || (marker.Value.ValueKind == JsonValueKind.Number && marker.Value.GetInt32() == 0));
    }

    [Fact]
    public async Task Fork_Nonexistent_Returns_404()
    {
        var huge = 2147480000; // unlikely to exist
        var ex = await Assert.ThrowsAsync<WordPressApiException>(() => _fx.RealApi.PostJsonAsync<object>("wp-json/rex/v1/fork", new { source_id = huge }, default));
        Assert.Equal(HttpStatusCode.NotFound, ex.StatusCode);
    }

    [Fact]
    public async Task Save_Ignores_Original_Meta()
    {
        var draft = await _fx.Core.CreatePostAsync(new CoreCreatePost
        {
            title = "IgnoreMeta",
            content = "body",
            status = "draft"
        });

        var res = await _svc.SaveAsync(draft.id, new SaveData("Ignore meta attempt", null, null, null, null, new Dictionary<string, object>{{"_rex_original_post_id", 12345}}, null, null));
        Assert.True(res.Saved is true);

        var after = await _fx.Core.GetPostAsync(draft.id, "id,meta,title");
        var marker = after.meta?.GetPropertyOrDefault("_rex_original_post_id");
        Assert.True(marker is null || marker.Value.ValueKind == JsonValueKind.Null || (marker.Value.ValueKind == JsonValueKind.Number && marker.Value.GetInt32() == 0));
    }

    [Fact]
    public async Task Taxonomy_Copy_On_Fork()
    {
        var cat = await _fx.Core.CreateCategoryAsync($"rex-cat-{DateTimeOffset.UtcNow.ToUnixTimeSeconds()}");
        var orig = await _fx.Core.CreatePostAsync(new CoreCreatePost
        {
            title = "Cats",
            content = "with cats",
            status = "publish",
            categories = new[] { cat.id }
        });

        var f = await _svc.ForkAsync(orig.id);
        var forkPost = await _fx.Core.GetPostAsync(f.Id, "id,categories");
        Assert.Contains(cat.id, forkPost.categories ?? Array.Empty<int>());
    }
}

// ===== Test fixture & helpers =====

public sealed class WpFixture : IDisposable
{
    public required Uri BaseUri { get; init; }
    public required string Username { get; init; }
    public required string AppPassword { get; init; }
    public bool VerifySsl { get; init; }

    public HttpClient Http { get; }
    public RealWordPressApiService RealApi { get; }
    public CoreClient Core { get; }

    public WpFixture()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WP_BASE_URL") ?? throw new InvalidOperationException("WP_BASE_URL not set");
        var user    = Environment.GetEnvironmentVariable("WP_USERNAME")  ?? throw new InvalidOperationException("WP_USERNAME not set");
        var app     = Environment.GetEnvironmentVariable("WP_APP_PASSWORD") ?? throw new InvalidOperationException("WP_APP_PASSWORD not set");
        var verify  = (Environment.GetEnvironmentVariable("WP_VERIFY_SSL") ?? "false").Trim().ToLowerInvariant() is "1" or "true" or "yes";

        BaseUri = new Uri(baseUrl.TrimEnd('/') + "/");
        Username = user;
        AppPassword = app;
        VerifySsl = verify;

        var handler = new HttpClientHandler();
        if (!VerifySsl)
        {
            handler.ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;
        }

        Http = new HttpClient(handler) { BaseAddress = BaseUri };
        var creds = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{Username}:{AppPassword}"));
        Http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", creds);
        Http.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        RealApi = new RealWordPressApiService(Http);
        Core    = new CoreClient(Http);
    }

    public WordPressEditingService CreateEditingService() => new(RealApi);

    public void Dispose() => Http.Dispose();
}

// Minimal real implementation of the API abstraction used by WordPressEditingService
public sealed class RealWordPressApiService : IWordPressApiService
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null // use attributes
    };

    public RealWordPressApiService(HttpClient http) => _http = http;

    public async Task<T?> PostJsonAsync<T>(string path, object body, CancellationToken ct = default)
    {
        using var content = new StringContent(JsonSerializer.Serialize(body, _json), Encoding.UTF8, "application/json");
        using var res = await _http.PostAsync(path, content, ct);
        var txt = await res.Content.ReadAsStringAsync(ct);
        if (!res.IsSuccessStatusCode)
            throw new WordPressApiException(res.StatusCode, txt);
        return typeof(T) == typeof(object) && string.IsNullOrWhiteSpace(txt)
            ? default
            : JsonSerializer.Deserialize<T>(txt, _json);
    }
}

public sealed class WordPressApiException : Exception
{
    public HttpStatusCode StatusCode { get; }
    public WordPressApiException(HttpStatusCode code, string body)
        : base($"HTTP {(int)code} {code}: {body}") => StatusCode = code;
}

// Core WP REST helpers (just what the tests need)
public sealed class CoreClient
{
    private readonly HttpClient _http;
    private readonly JsonSerializerOptions _json = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        PropertyNamingPolicy = null
    };

    public CoreClient(HttpClient http) => _http = http;

    public async Task<WpPost> CreatePostAsync(CoreCreatePost body)
    {
        var res = await _http.PostAsync("wp-json/wp/v2/posts", Json(body));
        await ThrowIfError(res);
        return await Deserialize<WpPost>(res);
    }

    public async Task<WpPost> GetPostAsync(int id, string? fields = null)
    {
        var url = $"wp-json/wp/v2/posts/{id}?context=edit" + (string.IsNullOrWhiteSpace(fields) ? string.Empty : $"&_fields={Uri.EscapeDataString(fields)}");
        var res = await _http.GetAsync(url);
        await ThrowIfError(res);
        return await Deserialize<WpPost>(res);
    }

    public async Task DeletePostAsync(int id, bool force)
    {
        var res = await _http.DeleteAsync($"wp-json/wp/v2/posts/{id}?force={(force ? "true" : "false")}");
        await ThrowIfError(res);
    }

    public async Task<WpCategory> CreateCategoryAsync(string name)
    {
        var res = await _http.PostAsync("wp-json/wp/v2/categories", Json(new { name }));
        await ThrowIfError(res);
        return await Deserialize<WpCategory>(res);
    }

    private StringContent Json(object o) => new StringContent(JsonSerializer.Serialize(o, _json), Encoding.UTF8, "application/json");

    private static async Task ThrowIfError(HttpResponseMessage res)
    {
        if (!res.IsSuccessStatusCode)
        {
            var body = await res.Content.ReadAsStringAsync();
            throw new WordPressApiException(res.StatusCode, body);
        }
    }

    private async Task<T> Deserialize<T>(HttpResponseMessage res)
        => (await JsonSerializer.DeserializeAsync<T>(await res.Content.ReadAsStreamAsync(), _json))!;
}

// ===== DTOs used in tests =====

public sealed class CoreCreatePost
{
    public string? title { get; set; }
    public string? content { get; set; }
    public string? status { get; set; }
    public int[]? categories { get; set; }
}

public sealed class WpRender
{
    public string? rendered { get; set; }
}

public sealed class WpPost
{
    public int id { get; set; }
    public string? status { get; set; }
    public WpRender? title { get; set; }
    public WpRender? content { get; set; }
    public string? modified_gmt { get; set; }
    public JsonElement? meta { get; set; }
    public int[]? categories { get; set; }
}

public sealed class WpCategory
{
    public int id { get; set; }
    public string? name { get; set; }
}

internal static class JsonElementExtensions
{
    public static JsonElement? GetPropertyOrDefault(this JsonElement? el, string name)
    {
        if (el is null) return null;
        var obj = el.Value;
        if (obj.ValueKind != JsonValueKind.Object) return null;
        return obj.TryGetProperty(name, out var v) ? v : (JsonElement?)null;
    }
}
