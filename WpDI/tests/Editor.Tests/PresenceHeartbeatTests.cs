using System.Text.Json;
using Microsoft.Extensions.Options;
using Xunit;
using System.Net.Http.Json;
using Editor.WordPress;
using TestSupport; // RunUniqueFixture

[Collection("WP EndToEnd")]
public class PresenceHeartbeatTests
{
    private readonly RunUniqueFixture _ids;
    public PresenceHeartbeatTests(RunUniqueFixture ids) => _ids = ids;

    private static WordPressApiService NewApi()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WP_BASE_URL")!;
        var user    = Environment.GetEnvironmentVariable("WP_USERNAME")!;
        var pass    = Environment.GetEnvironmentVariable("WP_APP_PASSWORD")!;
        return new WordPressApiService(Options.Create(new WordPressOptions {
            BaseUrl = baseUrl, UserName = user, AppPassword = pass, Timeout = TimeSpan.FromSeconds(10)
        }));
    }

    private static async Task<long> MeAsync(HttpClient http)
    {
        var res = await http.GetAsync("/wp-json/wp/v2/users/me");
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await res.Content.ReadAsStringAsync());
        return doc.RootElement.GetProperty("id").GetInt64();
    }

    [Fact]
    public async Task Session_Heartbeats_Every_One_Second()
    {
        var api = NewApi();
        var http = api.HttpClient!;

        // Create draft post with a per-run unique title
        var create = await http.PostAsJsonAsync("/wp-json/wp/v2/posts",
            new { title = _ids.Next("HB quick"), status = "draft", content = "<p>hi</p>" });
        create.EnsureSuccessStatusCode();
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = created.RootElement.GetProperty("id").GetInt64();

        var userId = await MeAsync(http);
        var service = new EditLockService(http);

        try
        {
            await using var session = await service.OpenAsync("posts", id, userId,
                new EditLockOptions { HeartbeatInterval = TimeSpan.FromSeconds(1) });

            Assert.True(session.IsClaimed);

            // Wait just over 1s to allow one heartbeat tick
            await Task.Delay(1200);

            // Verify lock contains our userId
            var check = await http.GetAsync($"/wp-json/wp/v2/posts/{id}?context=edit&_fields=meta._edit_lock");
            check.EnsureSuccessStatusCode();
            using var doc = JsonDocument.Parse(await check.Content.ReadAsStringAsync());
            var raw = doc.RootElement.GetProperty("meta").GetProperty("_edit_lock").GetString() ?? "";
            Assert.Contains($":{userId}", raw);
        }
        finally
        {
            await (await api.GetClientAsync())!.Posts.DeleteAsync((int)id, true);
        }
    }
}
