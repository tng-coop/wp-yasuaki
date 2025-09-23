using System.Net.Http.Json;
using System.Text.Json;
using Xunit;

namespace Editor.Tests;

[Collection("WP EndToEnd")]
public class EditingSmokeE2eTests
{
    private readonly WordPressCleanupFixture _fx;
    public EditingSmokeE2eTests(WordPressCleanupFixture fx) => _fx = fx;

    [Fact]
    public async Task Draft_Can_Be_Created_And_Is_Registered_For_Cleanup()
    {
        var http = _fx.Api.HttpClient!;
        var resp = await http.PostAsJsonAsync("/wp-json/wp/v2/posts", new {
            title = $"smoke-{Guid.NewGuid():N}",
            status = "draft",
            content = "<p>Hello</p>"
        });
        resp.EnsureSuccessStatusCode();

        using var json = JsonDocument.Parse(await resp.Content.ReadAsStringAsync());
        var id = json.RootElement.GetProperty("id").GetInt64();
        _fx.RegisterPost(id);

        Assert.True(id > 0);
    }
}
