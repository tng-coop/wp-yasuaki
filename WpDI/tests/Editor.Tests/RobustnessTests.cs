// tests/Editor.Tests/RobustnessTests.cs
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

[Collection("WP EndToEnd")]
public class RobustnessTests
{
    // ---------- Helpers (mirrors your existing style) ----------
    private static HttpClient NewClient(string baseUrl, bool allowInsecure = true)
    {
        if (allowInsecure && baseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            var handler = new HttpClientHandler
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };
            return new HttpClient(handler) { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(15) };
        }
        return new HttpClient { BaseAddress = new Uri(baseUrl), Timeout = TimeSpan.FromSeconds(15) };
    }

    private static void SetBasicAuth(HttpClient http, string user, string pass)
    {
        var token = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", token);
        http.DefaultRequestHeaders.Accept.ParseAdd("application/json");
    }

    private static (string BaseUrl, string User, string Pass) RequireEnv()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WP_BASE_URL");
        var user    = Environment.GetEnvironmentVariable("WP_USERNAME");
        var pass    = Environment.GetEnvironmentVariable("WP_APP_PASSWORD");

        Assert.False(string.IsNullOrWhiteSpace(baseUrl), "WP_BASE_URL is not set.");
        Assert.False(string.IsNullOrWhiteSpace(user),    "WP_USERNAME is not set.");
        Assert.False(string.IsNullOrWhiteSpace(pass),    "WP_APP_PASSWORD is not set.");

        return (baseUrl!, user!, pass!);
    }

    private static string OfficeRestBase()
        => Environment.GetEnvironmentVariable("WP_REST_BASE_OFFICE") ?? "office-cpt";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static IEnumerable<object[]> ContentTypes()
    {
        yield return new object[] { "posts" };
        yield return new object[] { OfficeRestBase() };
    }

    // ---------- A) Concurrent-ish edits → last-write-wins, revisions preserved ----------
    [Theory]
    [MemberData(nameof(ContentTypes))]
    public async Task LastWriteWins_ButRevisionsArePreserved(string type)
    {
        var (baseUrl, user, pass) = RequireEnv();
        using var http = NewClient(baseUrl);
        SetBasicAuth(http, user, pass);

        // Create a draft (or published for office frontpage; draft is fine here)
        object createPayload =
            type == "posts"
                ? new { title = $"Robustness LWW {Guid.NewGuid():N}", status = "draft", content = "<p>v0</p>" }
                : new { title = $"Office LWW {Guid.NewGuid():N}", status = "draft", data = new { blurb = "v0" } };

        var collectionPath = $"/wp-json/wp/v2/{type}";
        var createResp = await http.PostAsJsonAsync(collectionPath, createPayload, JsonOpts);
        Assert.Contains(createResp.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK });
        var created = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
        var id = created.RootElement.GetProperty("id").GetInt32();

        try
        {
            // First save (A)
            object payloadA =
                type == "posts"
                    ? new { content = "<p>version A</p>" }
                    : new { data = new { blurb = "version A" } };

            var saveA = await http.PostAsJsonAsync($"{collectionPath}/{id}", payloadA, JsonOpts);
            Assert.Equal(HttpStatusCode.OK, saveA.StatusCode);

            // Second save (B) — "later" update
            object payloadB =
                type == "posts"
                    ? new { content = "<p>version B</p>" }
                    : new { data = new { blurb = "version B" } };

            var saveB = await http.PostAsJsonAsync($"{collectionPath}/{id}", payloadB, JsonOpts);
            Assert.Equal(HttpStatusCode.OK, saveB.StatusCode);

            // Live content should reflect B
            var get = await http.GetAsync($"{collectionPath}/{id}?context=edit");
            Assert.Equal(HttpStatusCode.OK, get.StatusCode);
            var live = JsonDocument.Parse(await get.Content.ReadAsStringAsync());

            if (type == "posts")
            {
                var contentEl = live.RootElement.GetProperty("content");
                JsonElement raw;
                var hasRaw = contentEl.TryGetProperty("raw", out raw);
                var contentStr = hasRaw ? raw.GetString() : contentEl.GetProperty("rendered").GetString();
                Assert.Contains("version B", contentStr);
            }
            else
            {
                Assert.Equal("version B",
                    live.RootElement.GetProperty("data").GetProperty("blurb").GetString());
            }

            // Revisions should include at least one
            var revPath = $"/wp-json/wp/v2/{type}/{id}/revisions";
            var revResp = await http.GetAsync(revPath);
            Assert.True(revResp.StatusCode == HttpStatusCode.OK,
                $"GET {revPath} should succeed when revisions are enabled for '{type}'.");
            var revJson = await revResp.Content.ReadFromJsonAsync<JsonElement[]>() ?? Array.Empty<JsonElement>();
            Assert.True(revJson.Length >= 1, "At least one revision should be present after updates.");
        }
        finally
        {
            var del = await http.DeleteAsync($"{collectionPath}/{id}");
            Assert.Contains(del.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound });
        }
    }

    // ---------- B) Soft delete → Trash → Restore ----------
    [Theory]
    [MemberData(nameof(ContentTypes))]
    public async Task SoftDelete_MovesToTrash_AndRestore_Works(string type)
    {
        var (baseUrl, user, pass) = RequireEnv();
        using var http = NewClient(baseUrl);
        SetBasicAuth(http, user, pass);

        var collectionPath = $"/wp-json/wp/v2/{type}";
        object createPayload =
            type == "posts"
                ? new { title = $"Trash Test {Guid.NewGuid():N}", status = "draft", content = "<p>hello</p>" }
                : new { title = $"Office Trash {Guid.NewGuid():N}", status = "draft", data = new { blurb = "hello" } };

        var create = await http.PostAsJsonAsync(collectionPath, createPayload, JsonOpts);
        Assert.Contains(create.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK });
        var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = created.RootElement.GetProperty("id").GetInt32();

        try
        {
            // Soft delete (no ?force=true)
            var del = await http.DeleteAsync($"{collectionPath}/{id}");
            Assert.Contains(del.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NoContent });

            var getTrash = await http.GetAsync($"{collectionPath}/{id}?context=edit");
            if (getTrash.StatusCode == HttpStatusCode.OK)
            {
                var trashed = JsonDocument.Parse(await getTrash.Content.ReadAsStringAsync());
                Assert.Equal("trash", trashed.RootElement.GetProperty("status").GetString());
            }

            // Restore by setting status back to draft
            var restore = await http.PostAsJsonAsync($"{collectionPath}/{id}", new { status = "draft" }, JsonOpts);
            Assert.Equal(HttpStatusCode.OK, restore.StatusCode);

            // Verify restored
            var getBack = await http.GetAsync($"{collectionPath}/{id}?context=edit");
            Assert.Equal(HttpStatusCode.OK, getBack.StatusCode);
            var doc = JsonDocument.Parse(await getBack.Content.ReadAsStringAsync());
            Assert.Equal("draft", doc.RootElement.GetProperty("status").GetString());
        }
        finally
        {
            var hard = await http.DeleteAsync($"{collectionPath}/{id}?force=true");
            Assert.Contains(hard.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound });
        }
    }
}

