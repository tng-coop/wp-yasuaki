// WpDI/tests/Editor.Tests/RobustnessTests.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using WordPressPCL;
using WordPressPCL.Models;
using Xunit;
using Editor.WordPress; // WordPressApiService, WordPressOptions
using TestSupport;      // RunUniqueFixture

[Collection("WP EndToEnd")]
public class RobustnessTests
{
    private readonly RunUniqueFixture _ids;
    public RobustnessTests(RunUniqueFixture ids) => _ids = ids;

    // ---------- Service wiring ----------
    private static WordPressApiService NewApi()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WP_BASE_URL");
        var user    = Environment.GetEnvironmentVariable("WP_USERNAME");
        var pass    = Environment.GetEnvironmentVariable("WP_APP_PASSWORD");

        Assert.False(string.IsNullOrWhiteSpace(baseUrl), "WP_BASE_URL is not set.");
        Assert.False(string.IsNullOrWhiteSpace(user),    "WP_USERNAME is not set.");
        Assert.False(string.IsNullOrWhiteSpace(pass),    "WP_APP_PASSWORD is not set.");

        var opts = Options.Create(new WordPressOptions
        {
            BaseUrl     = baseUrl!,
            UserName    = user!,
            AppPassword = pass!,
            Timeout     = TimeSpan.FromSeconds(20)
        });
        return new WordPressApiService(opts);
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
        yield return new object[] { "posts", true };
        yield return new object[] { OfficeRestBase(), false };
    }

    // ---------- A) Concurrent-ish edits → last-write-wins, revisions preserved ----------
    [Theory]
    [MemberData(nameof(ContentTypes))]
    public async Task LastWriteWins_ButRevisionsArePreserved(string type, bool isPosts)
    {
        var api  = NewApi();
        var http = api.HttpClient!;
        var wpcl = (await api.GetClientAsync())!;

        var collectionPath = $"/wp-json/wp/v2/{type}";
        int? id = null;

        try
        {
            // Create draft
            if (isPosts)
            {
                var post = new Post
                {
                    Title   = new Title(_ids.Next("Robustness LWW")),
                    Status  = Status.Draft,
                    Content = new Content("<p>v0</p>")
                };
                var created = await wpcl.Posts.CreateAsync(post);
                Assert.NotNull(created);
                id = created!.Id;
            }
            else
            {
                var payload = new
                {
                    title = _ids.Next("Office LWW"),
                    status = "draft",
                    data = new { blurb = "v0" }
                };
                var resp = await http.PostAsJsonAsync(collectionPath, payload, JsonOpts);
                Assert.Contains(resp.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK });
                var created = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                id = created.RootElement.GetProperty("id").GetInt32();
            }

            // First save
            if (isPosts)
            {
                var p = await wpcl.Posts.GetByIDAsync(id!.Value);
                p!.Content ??= new Content();
                p.Content.Raw = "<p>version A</p>";
                await wpcl.Posts.UpdateAsync(p);
            }
            else
            {
                var payloadA = new { data = new { blurb = "version A" } };
                var resp = await http.PostAsJsonAsync($"{collectionPath}/{id}", payloadA, JsonOpts);
                Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            }

            // Second save
            if (isPosts)
            {
                var p = await wpcl.Posts.GetByIDAsync(id!.Value);
                p!.Content ??= new Content();
                p.Content.Raw = "<p>version B</p>";
                await wpcl.Posts.UpdateAsync(p);
            }
            else
            {
                var payloadB = new { data = new { blurb = "version B" } };
                var resp = await http.PostAsJsonAsync($"{collectionPath}/{id}", payloadB, JsonOpts);
                Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
            }

            // Verify content
            if (isPosts)
            {
                var live = await wpcl.Posts.GetByIDAsync(id!.Value);
                var text = live!.Content?.Rendered ?? live.Content?.Raw ?? "";
                Assert.Contains("version B", text);
            }
            else
            {
                var get = await http.GetAsync($"{collectionPath}/{id}?context=edit");
                Assert.Equal(HttpStatusCode.OK, get.StatusCode);
                var live = JsonDocument.Parse(await get.Content.ReadAsStringAsync());
                Assert.Equal("version B", live.RootElement.GetProperty("data").GetProperty("blurb").GetString());
            }

            // Verify revisions
            var revResp = await http.GetAsync($"/wp-json/wp/v2/{type}/{id}/revisions");
            Assert.Equal(HttpStatusCode.OK, revResp.StatusCode);
            var revs = await revResp.Content.ReadFromJsonAsync<JsonElement[]>() ?? Array.Empty<JsonElement>();
            Assert.True(revs.Length >= 1);
        }
        finally
        {
            if (id.HasValue)
            {
                if (isPosts)
                    await wpcl.Posts.DeleteAsync(id.Value, true);
                else
                    await http.DeleteAsync($"{collectionPath}/{id.Value}?force=true");
            }
        }
    }

    // ---------- B) Soft delete → Trash → Restore ----------
    [Theory]
    [MemberData(nameof(ContentTypes))]
    public async Task SoftDelete_MovesToTrash_AndRestore_Works(string type, bool isPosts)
    {
        var api  = NewApi();
        var http = api.HttpClient!;
        var wpcl = (await api.GetClientAsync())!;

        var collectionPath = $"/wp-json/wp/v2/{type}";
        int? id = null;

        try
        {
            // Create draft
            if (isPosts)
            {
                var post = new Post
                {
                    Title   = new Title(_ids.Next("Trash Test")),
                    Status  = Status.Draft,
                    Content = new Content("<p>hello</p>")
                };
                var created = await wpcl.Posts.CreateAsync(post);
                id = created!.Id;
            }
            else
            {
                var payload = new
                {
                    title = _ids.Next("Office Trash"),
                    status = "draft",
                    data = new { blurb = "hello" }
                };
                var resp = await http.PostAsJsonAsync(collectionPath, payload, JsonOpts);
                Assert.Contains(resp.StatusCode, new[] { HttpStatusCode.Created, HttpStatusCode.OK });
                var created = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
                id = created.RootElement.GetProperty("id").GetInt32();
            }

            // Trash
            if (isPosts)
            {
                var trashed = await wpcl.Posts.DeleteAsync(id!.Value, false);
                Assert.True(trashed);
            }
            else
            {
                var del = await http.DeleteAsync($"{collectionPath}/{id}");
                Assert.Contains(del.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NoContent });
            }

            // Restore (use raw REST for a simple status flip)
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
            if (id.HasValue)
            {
                if (isPosts)
                    await wpcl.Posts.DeleteAsync(id.Value, true);
                else
                    await http.DeleteAsync($"{collectionPath}/{id.Value}?force=true");
            }
        }
    }
}
