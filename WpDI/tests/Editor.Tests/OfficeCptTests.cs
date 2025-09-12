// WpDI/tests/Editor.Tests/OfficeCptTests.cs
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Xunit;

[Collection("WP EndToEnd")]
public class OfficeCptTests
{
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

    private static (string BaseUrl, string User, string Pass, string RestBase) RequireEnv()
    {
        var baseUrl  = Environment.GetEnvironmentVariable("WP_BASE_URL");
        var user     = Environment.GetEnvironmentVariable("WP_USERNAME");
        var pass     = Environment.GetEnvironmentVariable("WP_APP_PASSWORD");
        var restBase = Environment.GetEnvironmentVariable("WP_REST_BASE_OFFICE") ?? "office-cpt";

        Assert.False(string.IsNullOrWhiteSpace(baseUrl), "WP_BASE_URL is not set.");
        Assert.False(string.IsNullOrWhiteSpace(user),    "WP_USERNAME is not set.");
        Assert.False(string.IsNullOrWhiteSpace(pass),    "WP_APP_PASSWORD is not set.");
        return (baseUrl!, user!, pass!, restBase);
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    [Fact]
    public async Task OfficeCpt_CRUD_Works_With_Data_Meta()
    {
        var (baseUrl, user, pass, restBase) = RequireEnv();
        using var http = NewClient(baseUrl);
        SetBasicAuth(http, user, pass);

        var collectionPath = $"/wp-json/wp/v2/{restBase}";

        // 1) CREATE
        var createPayload = new
        {
            title = "Tokyo HQ",
            status = "publish",
            data = new { address = "1-1 Chiyoda, Tokyo", floors = 12, tags = new[] { "apac", "r&d" } }
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
            Assert.Equal("1-1 Chiyoda, Tokyo", dataObj.GetProperty("address").GetString());
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
            // 4) DELETE
            var delResp = await http.DeleteAsync($"{collectionPath}/{officeId}?force=true");
            Assert.Contains(delResp.StatusCode, new[] { HttpStatusCode.OK, HttpStatusCode.NoContent, HttpStatusCode.NotFound });
        }
    }
}
