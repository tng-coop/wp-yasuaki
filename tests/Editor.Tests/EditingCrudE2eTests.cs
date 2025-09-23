using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Editor.WordPress;
using Microsoft.Extensions.Options;
using TestSupport; // RunUniqueFixture
using Xunit;

namespace Editor.Tests;

[Collection("WP EndToEnd")]
public class EditingCrudE2eTests
{
    private readonly WordPressCleanupFixture _fx;
    private readonly RunUniqueFixture _ids;

    public EditingCrudE2eTests(WordPressCleanupFixture fx, RunUniqueFixture ids)
    {
        _fx = fx;
        _ids = ids;
    }

    private static readonly JsonSerializerOptions JsonWeb = new(JsonSerializerDefaults.Web)
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    [Fact]
    public async Task CRUD_EndToEnd_For_Posts()
    {
        // Arrange: API client from fixture
        var http = _fx.Api.HttpClient!;
        var title = _ids.Next("CRUD Post");
        long id = 0;

        // ---------- Create (draft) ----------
        var createResp = await http.PostAsJsonAsync("/wp-json/wp/v2/posts", new
        {
            title,
            status = "draft",
            content = "<p>v0</p>"
        }, JsonWeb);
        createResp.EnsureSuccessStatusCode();

        using (var created = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync()))
        {
            id = created.RootElement.GetProperty("id").GetInt64();
        }
        Assert.True(id > 0, "New post must have an ID");
        _fx.RegisterPost(id); // ensure cleanup

        // ---------- Read (context=edit) ----------
        var readResp = await http.GetAsync($"/wp-json/wp/v2/posts/{id}?context=edit");
        readResp.EnsureSuccessStatusCode();
        var readJson = JsonDocument.Parse(await readResp.Content.ReadAsStringAsync());
        Assert.Equal("draft", readJson.RootElement.GetProperty("status").GetString());

        // ---------- Update content + title ----------
        var newTitle = $"{title} (updated)";
        var updResp = await http.PostAsJsonAsync($"/wp-json/wp/v2/posts/{id}", new
        {
            title = newTitle,
            content = "<p>v1</p>"
        }, JsonWeb);
        updResp.EnsureSuccessStatusCode();

        var updJson = JsonDocument.Parse(await updResp.Content.ReadAsStringAsync());
        var renderedTitle = updJson.RootElement.GetProperty("title").GetProperty("rendered").GetString() ?? "";
        Assert.Contains("updated", renderedTitle, StringComparison.OrdinalIgnoreCase);

        // ---------- Autosave ----------
        var autosaveResp = await http.PostAsJsonAsync($"/wp-json/wp/v2/posts/{id}/autosaves", new
        {
            title = newTitle,
            content = "<p>autosave v2</p>"
        }, JsonWeb);
        autosaveResp.EnsureSuccessStatusCode();

        // Quick sanity: fetch revisions — should be >= 1
        var revs = await http.GetFromJsonAsync<JsonElement[]>($"/wp-json/wp/v2/posts/{id}/revisions");
        Assert.NotNull(revs);
        Assert.True(revs!.Length >= 1);

        // ---------- Publish ----------
        var pubResp = await http.PostAsJsonAsync($"/wp-json/wp/v2/posts/{id}", new { status = "publish" }, JsonWeb);
        pubResp.EnsureSuccessStatusCode();

        var pubJson = JsonDocument.Parse(await pubResp.Content.ReadAsStringAsync());
        Assert.Equal("publish", pubJson.RootElement.GetProperty("status").GetString());

        // ---------- Switch back to Draft ----------
        var draftResp = await http.PostAsJsonAsync($"/wp-json/wp/v2/posts/{id}", new { status = "draft" }, JsonWeb);
        draftResp.EnsureSuccessStatusCode();

        var draftJson = JsonDocument.Parse(await draftResp.Content.ReadAsStringAsync());
        Assert.Equal("draft", draftJson.RootElement.GetProperty("status").GetString());

        // ---------- Soft delete (Trash) ----------
        var trashResp = await http.DeleteAsync($"/wp-json/wp/v2/posts/{id}");
        Assert.True(
            trashResp.StatusCode is HttpStatusCode.OK or HttpStatusCode.NoContent,
            $"Trash should succeed, got {trashResp.StatusCode}"
        );

        // Verify it’s in trash (status=trash)
        var readTrash = await http.GetAsync($"/wp-json/wp/v2/posts/{id}?context=edit");
        Assert.Equal(HttpStatusCode.OK, readTrash.StatusCode);
        var trashJson = JsonDocument.Parse(await readTrash.Content.ReadAsStringAsync());
        Assert.Equal("trash", trashJson.RootElement.GetProperty("status").GetString());

        // ---------- Restore from Trash (status=draft) ----------
        var restoreResp = await http.PostAsJsonAsync($"/wp-json/wp/v2/posts/{id}", new { status = "draft" }, JsonWeb);
        restoreResp.EnsureSuccessStatusCode();

        var restoreJson = JsonDocument.Parse(await restoreResp.Content.ReadAsStringAsync());
        Assert.Equal("draft", restoreJson.RootElement.GetProperty("status").GetString());

        // Note: Hard delete is handled by the fixture on teardown (force=true).
    }

    [Fact]
    public async Task Categories_List_Basic_Smoke()
    {
        var http = _fx.Api.HttpClient!;
        // small page pull to ensure it responds
        var res = await http.GetAsync("/wp-json/wp/v2/categories?per_page=5&page=1&_fields=id,name,parent");
        res.EnsureSuccessStatusCode();
        var arr = await res.Content.ReadFromJsonAsync<JsonElement[]>() ?? Array.Empty<JsonElement>();
        Assert.True(arr.Length >= 0); // just ensure endpoint is alive
    }

    [Fact]
    public async Task Revisions_Exist_After_Multiple_Updates()
    {
        var http = _fx.Api.HttpClient!;
        var title = _ids.Next("CRUD Rev");
        long id = 0;

        try
        {
            // Create draft
            var create = await http.PostAsJsonAsync("/wp-json/wp/v2/posts", new { title, status = "draft", content = "<p>v0</p>" }, JsonWeb);
            create.EnsureSuccessStatusCode();
            id = JsonDocument.Parse(await create.Content.ReadAsStringAsync()).RootElement.GetProperty("id").GetInt64();
            _fx.RegisterPost(id);

            // Two updates to generate revisions
            async Task UpdateTo(string html)
            {
                var r = await http.PostAsJsonAsync($"/wp-json/wp/v2/posts/{id}", new { content = html }, JsonWeb);
                r.EnsureSuccessStatusCode();
            }

            await UpdateTo("<p>v1</p>");
            await UpdateTo("<p>v2</p>");

            // Verify revisions ≥ 1 (some installs keep autosave; some revisions plugin behavior varies)
            var revs = await http.GetFromJsonAsync<JsonElement[]>($"/wp-json/wp/v2/posts/{id}/revisions") ?? Array.Empty<JsonElement>();
            Assert.True(revs.Length >= 1);
        }
        finally
        {
            // fixture will force-delete
        }
    }
}
