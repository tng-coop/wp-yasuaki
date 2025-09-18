using System.Text.Json;
using Microsoft.Extensions.Options;
using Xunit;
using Editor.WordPress;
using System.Net.Http.Json;
using TestSupport; // RunUniqueFixture

[Collection("WP EndToEnd")]
public class PresenceQuickTests
{
    private readonly RunUniqueFixture _ids;
    public PresenceQuickTests(RunUniqueFixture ids) => _ids = ids;

    private static WordPressApiService NewApi()
    {
        var baseUrl = Environment.GetEnvironmentVariable("WP_BASE_URL")!;
        var user    = Environment.GetEnvironmentVariable("WP_USERNAME")!;
        var pass    = Environment.GetEnvironmentVariable("WP_APP_PASSWORD")!;
        return new WordPressApiService(Options.Create(new WordPressOptions
        {
            BaseUrl = baseUrl,
            UserName = user,
            AppPassword = pass,
            Timeout = TimeSpan.FromSeconds(10)
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
    public async Task Claim_ReadAndRelease_AreFast()
    {
        var api  = NewApi();
        var httpA = api.HttpClient!;
        var apiB  = NewApi(); // second client with the same app password
        var httpB = apiB.HttpClient!;

        // Create draft post with per-run unique title
        var title  = _ids.Next("WPDI lock quick");
        var create = await httpA.PostAsJsonAsync("/wp-json/wp/v2/posts",
            new { title, status = "draft", content = "<p>hi</p>" });
        create.EnsureSuccessStatusCode();
        using var created = JsonDocument.Parse(await create.Content.ReadAsStringAsync());
        var id = created.RootElement.GetProperty("id").GetInt64();

        var userA  = await MeAsync(httpA);
        var locksA = new EditLockService(httpA);
        var locksB = new EditLockService(httpB);

        try
        {
            // A claims (no timer for speed)
            await using var session = await locksA.OpenAsync("posts", id, userA,
                new EditLockOptions { HeartbeatInterval = TimeSpan.Zero });
            Assert.True(session.IsClaimed);

            // B sees lock immediately
            var check = await httpB.GetAsync($"/wp-json/wp/v2/posts/{id}?context=edit&_fields=meta._edit_lock");
            check.EnsureSuccessStatusCode();
            using var seenDoc = JsonDocument.Parse(await check.Content.ReadAsStringAsync());
            var raw = seenDoc.RootElement.GetProperty("meta").GetProperty("_edit_lock").GetString() ?? "";
            Assert.Contains($":{userA}", raw);

            // Dispose releases
        }
        finally
        {
            await (await api.GetClientAsync())!.Posts.DeleteAsync((int)id, true);
        }
    }
}
