using System.Text.Json;
using Microsoft.Extensions.Options;
using Xunit;
using System.Net.Http.Json;
using Editor.WordPress;
using TestSupport; // RunUniqueFixture

[Collection("WP EndToEnd")]
public class PresenceCrashTests
{
    private readonly RunUniqueFixture _ids;
    public PresenceCrashTests(RunUniqueFixture ids) => _ids = ids;

    private static WordPressApiService NewApi()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WP_BASE_URL")!;
        var user    = Environment.GetEnvironmentVariable("WP_USERNAME")!;
        var pass    = Environment.GetEnvironmentVariable("WP_APP_PASSWORD")!;
        return new WordPressApiService(Options.Create(new WordPressOptions
        {
            BaseUrl = baseUrl, UserName = user, AppPassword = pass, Timeout = TimeSpan.FromSeconds(10)
        }));
    }

    [Fact]
    public async Task AgingLock_AllowsImmediateTakeover()
    {
        var api = NewApi();
        var http = api.HttpClient!;

        // Create post with per-run unique title
        var create = await http.PostAsJsonAsync("/wp-json/wp/v2/posts",
            new { title = _ids.Next("Crash case"), status = "draft", content = "<p>x</p>" });
        create.EnsureSuccessStatusCode();
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = created.RootElement.GetProperty("id").GetInt64();

        try
        {
            // Get current user
            var meRes = await http.GetAsync("/wp-json/wp/v2/users/me");
            meRes.EnsureSuccessStatusCode();
            var myId = JsonDocument.Parse(await meRes.Content.ReadAsStringAsync())
                                   .RootElement.GetProperty("id").GetInt64();

            var service = new EditLockService(http);

            // Claim without heartbeat
            await using (await service.OpenAsync("posts", id, myId,
                new EditLockOptions { HeartbeatInterval = TimeSpan.Zero })) { }

            // Age lock: set very old timestamp (simulate crash without waiting)
            var oldTs = DateTimeOffset.UtcNow.AddHours(-1).ToUnixTimeSeconds();
            var payload = new { meta = new { _edit_lock = $"{oldTs}:{myId}" } };
            var res = await http.PostAsJsonAsync($"/wp-json/wp/v2/posts/{id}", payload);
            res.EnsureSuccessStatusCode();

            // Takeover immediately (same user for simplicity)
            await using var takeover = await service.OpenAsync("posts", id, myId,
                new EditLockOptions { HeartbeatInterval = TimeSpan.Zero });
            Assert.True(takeover.IsClaimed);
        }
        finally
        {
            await (await api.GetClientAsync())!.Posts.DeleteAsync((int)id, true);
        }
    }
}
