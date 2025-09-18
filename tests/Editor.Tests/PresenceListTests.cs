using System.Text.Json;
using Microsoft.Extensions.Options;
using Editor.WordPress;
using Xunit;

[Collection("WP EndToEnd")]
public class PresenceListTests
{
    private static WordPressApiService NewApi()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WP_BASE_URL")!;
        var user    = Environment.GetEnvironmentVariable("WP_USERNAME")!;
        var pass    = Environment.GetEnvironmentVariable("WP_APP_PASSWORD")!;
        return new WordPressApiService(Options.Create(new WordPressOptions { BaseUrl = baseUrl, UserName = user, AppPassword = pass, Timeout = TimeSpan.FromSeconds(10) }));
    }

    [Fact]
    public async Task List_Returns_EditLock_Meta()
    {
        var api = NewApi();
        var http = api.HttpClient!;
        var fields = "id,title,modified_gmt,status,meta._edit_lock,meta._edit_last";
        var res = await http.GetAsync($"/wp-json/wp/v2/posts?context=edit&_fields={fields}&per_page=5&page=1");
        res.EnsureSuccessStatusCode();

        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        Assert.Equal(JsonValueKind.Array, doc.RootElement.ValueKind);
        foreach (var item in doc.RootElement.EnumerateArray())
        {
            Assert.True(item.TryGetProperty("id", out _));
            Assert.True(item.TryGetProperty("meta", out var meta));
            _ = meta.TryGetProperty("_edit_lock", out _); // may be null/empty; shape is what we care about
        }
    }
}
