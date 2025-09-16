// WpDI/tests/Editor.Tests/OfficeCptTests.cs
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;
using Xunit;
using Editor.WordPress; // WordPressApiService, WordPressOptions
using TestSupport;      // RunUniqueFixture

[Collection("WP EndToEnd")]
public class OfficeCptTests
{
    private readonly RunUniqueFixture _ids;
    public OfficeCptTests(RunUniqueFixture ids) => _ids = ids;

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
            Timeout     = TimeSpan.FromSeconds(15)
        });
        return new WordPressApiService(opts);
    }

    private static string OfficeRestBase()
        => Environment.GetEnvironmentVariable("WP_REST_BASE_OFFICE") ?? "office-cpt";

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task OfficeCpt_CRUD_Works_With_Data_Meta()
    {
        var api  = NewApi();
        var http = api.HttpClient!;
        var restBase = OfficeRestBase();
        var collectionPath = $"/wp-json/wp/v2/{restBase}";

        // 1) CREATE (unique, readable title per run)
        var createPayload = new
        {
            title = _ids.Next("Tokyo HQ"),
            status = "publish",
            data = new { address = $"1-1 Chiyoda, Tokyo ({_ids.RunId})", floors = 12, tags = new[] { "apac", "r&d" } }
        };

        var createResp = await http.PostAsJsonAsync(collectionPath, createPayload, JsonOpts);
        Assert.True(createResp.StatusCode == HttpStatusCode.Created,
            $"POST {collectionPath} should succeed (got {createResp.StatusCode}).");
        var createdJson = JsonDocument.Parse(await createResp.Content.ReadAsStringAsync());
        var officeId = createdJson.RootElement.GetProperty("id").GetInt32();
        Assert.True(officeId > 0);

        try
        {
            // 2) READ
            var getResp = await http.GetAsync($"{collectionPath}/{officeId}?context=edit");
            Assert.Equal(HttpStatusCode.OK, getResp.StatusCode);
            var getDoc = JsonDocument.Parse(await getResp.Content.ReadAsStringAsync());

            var rendered = getDoc.RootElement.GetProperty("title").GetProperty("rendered").GetString();
            Assert.Contains("Tokyo HQ", rendered);

            var dataObj = getDoc.RootElement.GetProperty("data");
            Assert.Contains("1-1 Chiyoda, Tokyo", dataObj.GetProperty("address").GetString() ?? "");
            Assert.Equal(12, dataObj.GetProperty("floors").GetInt32());

            // 3) UPDATE
            var updatePayload = new { data = new { address = "2-2 Chiyoda, Tokyo", floors = 13 } };
            var updResp = await http.PostAsJsonAsync($"{collectionPath}/{officeId}", updatePayload, JsonOpts);
            Assert.Equal(HttpStatusCode.OK, updResp.StatusCode);

            var afterDoc = JsonDocument.Parse(await updResp.Content.ReadAsStringAsync());
            Assert.Equal("2-2 Chiyoda, Tokyo", afterDoc.RootElement.GetProperty("data").GetProperty("address").GetString());
            Assert.Equal(13, afterDoc.RootElement.GetProperty("data").GetProperty("floors").GetInt32());
        }
        finally
        {
            // 4) DELETE (force to avoid Trash)
            var delResp = await http.DeleteAsync($"{collectionPath}/{officeId}?force=true");
            Assert.Contains(delResp.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound });
        }
    }
}
