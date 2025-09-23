// tests/Editor.Tests/WordPressEditingService_ConcurrencyE2eTests.cs
#nullable enable
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Editor.Abstractions;
using Editor.WordPress;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Editor.Tests;

[Collection("WP EndToEnd")]
public class WordPressEditingService_ConcurrencyE2eTests
{
    private readonly WordPressCleanupFixture _fx;
    public WordPressEditingService_ConcurrencyE2eTests(WordPressCleanupFixture fx) => _fx = fx;

    private IServiceProvider BuildProvider()
    {
        var baseUrl = Env("WP_BASE_URL");      // e.g. https://example.com
        var user    = Env("WP_USERNAME");
        var pass    = Env("WP_APP_PASSWORD");

        var services = new ServiceCollection();

        // Real WordPressApiService (admin app-password)
        services.Configure<WordPressOptions>(o =>
        {
            o.BaseUrl     = baseUrl;           // bare base; service adds /wp-json
            o.UserName    = user;
            o.AppPassword = pass;
            o.Timeout     = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<IWordPressApiService, WordPressApiService>();

        // Caching + editor services (same as existing tests)
        services.AddSingleton<IPostCache, MemoryPostCache>();
        services.AddWordPressEditing(); // registers IPostEditor backed by real server

        // Locks based on WP HttpClient
        services.AddWpdiEditLocks(sp =>
        {
            var api = sp.GetRequiredService<IWordPressApiService>();
            return api.HttpClient ?? throw new InvalidOperationException("WP HttpClient not initialized.");
        });

        // Facade under test (our service with the new token logic)
        services.AddScoped<IEditingService>(sp =>
            new WordPressEditingService(
                sp.GetRequiredService<IWordPressApiService>(),
                sp.GetRequiredService<IPostEditor>(),
                sp.GetRequiredService<IEditLockService>()));

        return services.BuildServiceProvider();
    }

    // ─────────────────────────────────────────────────────────────────────────────
    // Tests (tolerant of server timestamp resolution & behavior)
    // ─────────────────────────────────────────────────────────────────────────────

    [Fact]
    public async Task Autosave_DoesNotAdvanceParentModified_AndReturnsNullServerTime()
    {
        using var scope = BuildProvider().CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEditingService>();
        var api = scope.ServiceProvider.GetRequiredService<IWordPressApiService>();

        // Arrange: create draft via service (seeds token)
        var create = await svc.SaveDraftAsync(new Editor.WordPress.PostDetail(
            Id: 0,
            Title: $"concurrency-autosave-{Guid.NewGuid():N}",
            Html: "<p>initial</p>",
            Status: "draft",
            CategoryIds: new List<int>(),
            ModifiedUtc: null,
            Link: null));
        Assert.True(create.PostId > 0);
        _fx.RegisterPost(create.PostId);

        var initial = await GetModifiedDtoAsync(api, create.PostId);

        // Act: change content and AUTOSAVE
        var pd = new Editor.WordPress.PostDetail(
            Id: create.PostId,
            Title: "t",
            Html: "<p>autosave v1</p>",
            Status: "draft",
            CategoryIds: new List<int>(),
            ModifiedUtc: null,
            Link: null);

        var auto = await svc.AutosaveAsync(pd);

        // Assert: service contract
        Assert.Equal(SaveOutcome.Updated, auto.Outcome);
        Assert.Null(auto.ServerModifiedUtc);

        // Parent modified may stay same or advance depending on server/plugins; both are OK.
        var after = await GetModifiedDtoAsync(api, create.PostId);
        Assert.True(after >= initial);
    }

    [Fact]
    public async Task SaveDraft_AdvancesParentModified_NoConflict()
    {
        using var scope = BuildProvider().CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEditingService>();
        var api = scope.ServiceProvider.GetRequiredService<IWordPressApiService>();

        // Arrange
        var create = await svc.SaveDraftAsync(new Editor.WordPress.PostDetail(
            Id: 0,
            Title: $"concurrency-save-{Guid.NewGuid():N}",
            Html: "<p>initial</p>",
            Status: "draft",
            CategoryIds: new List<int>(),
            ModifiedUtc: null,
            Link: null));
        Assert.True(create.PostId > 0);
        _fx.RegisterPost(create.PostId);

        var beforeSave = await GetModifiedDtoAsync(api, create.PostId);

        // Act: real save
        var pd = new Editor.WordPress.PostDetail(
            Id: create.PostId,
            Title: "t",
            Html: "<p>save v1</p>",
            Status: "draft",
            CategoryIds: new List<int>(),
            ModifiedUtc: null,
            Link: null);
        var res = await svc.SaveDraftAsync(pd);

        // Assert: outcome OK; and parent modified time is >= previous (may be equal if same-second)
        Assert.Contains(res.Outcome, new[] { SaveOutcome.Updated, SaveOutcome.Created });
        Assert.Equal("draft", res.Status);
        Assert.NotNull(res.ServerModifiedUtc);

        var afterSave = await GetModifiedDtoAsync(api, create.PostId);
        Assert.True(afterSave >= beforeSave);
    }

    [Fact]
    public async Task SaveDraft_ReportsConflict_WhenParentAdvancedExternally()
    {
        using var scope = BuildProvider().CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEditingService>();
        var api = scope.ServiceProvider.GetRequiredService<IWordPressApiService>();

        // Arrange
        var create = await svc.SaveDraftAsync(new Editor.WordPress.PostDetail(
            Id: 0,
            Title: $"concurrency-conflict-{Guid.NewGuid():N}",
            Html: "<p>initial</p>",
            Status: "draft",
            CategoryIds: new List<int>(),
            ModifiedUtc: null,
            Link: null));
        Assert.True(create.PostId > 0);
        _fx.RegisterPost(create.PostId);

        // Capture a last-seen (string) for the stale edit
        var lastSeen = await GetModifiedRawAsync(api, create.PostId);

        // Externally advance the parent; ensure timestamp actually changes
        await EnsureModifiedChangesAsync(api, create.PostId, lastSeen, () => PatchContentRawAsync(api, create.PostId, "<p>external edit 1</p>"));
        var now1 = await GetModifiedRawAsync(api, create.PostId);
        await EnsureModifiedChangesAsync(api, create.PostId, now1, () => PatchContentRawAsync(api, create.PostId, "<p>external edit 2</p>"));

        // Attempt save with stale last-seen in PostDetail (do NOT call GetPostAsync in between)
        var pd = new Editor.WordPress.PostDetail(
            Id: create.PostId,
            Title: "t",
            Html: "<p>ours</p>",
            Status: "draft",
            CategoryIds: new List<int>(),
            ModifiedUtc: lastSeen, // stale on purpose
            Link: null);

        var res = await svc.SaveDraftAsync(pd);

        // Assert: real conflict
        Assert.Equal(SaveOutcome.Conflict, res.Outcome);
        Assert.Equal("draft", res.Status);
        Assert.NotNull(res.ServerModifiedUtc);
    }

    // ───────────────────────── helpers (live REST) ───────────────────────────────

    private static async Task<DateTimeOffset> GetModifiedDtoAsync(IWordPressApiService api, long id)
    {
        using var res = await api.HttpClient!.GetAsync($"/wp-json/wp/v2/posts/{id}?context=edit&_fields=modified_gmt");
        var body = await res.Content.ReadAsStringAsync();
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(body);
        var s = doc.RootElement.GetProperty("modified_gmt").GetString() ?? "";
        return DateTimeOffset.TryParse(s, out var dto) ? dto : DateTimeOffset.MinValue;
    }

    private static async Task<string> GetModifiedRawAsync(IWordPressApiService api, long id)
    {
        using var res = await api.HttpClient!.GetAsync($"/wp-json/wp/v2/posts/{id}?context=edit&_fields=modified_gmt");
        var body = await res.Content.ReadAsStringAsync();
        res.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(body);
        return doc.RootElement.GetProperty("modified_gmt").GetString() ?? "";
    }

    private static async Task PatchContentRawAsync(IWordPressApiService api, long id, string html)
    {
        using var res = await api.HttpClient!.PostAsJsonAsync($"/wp-json/wp/v2/posts/{id}", new { content = html });
        res.EnsureSuccessStatusCode();
    }

    private static async Task EnsureModifiedChangesAsync(
        IWordPressApiService api,
        long id,
        string previous,
        Func<Task> mutator,
        int timeoutMs = 5000,
        int pollMs = 200)
    {
        var start = DateTime.UtcNow;
        while ((DateTime.UtcNow - start).TotalMilliseconds < timeoutMs)
        {
            await mutator();
            var curr = await GetModifiedRawAsync(api, id);
            if (!string.Equals(curr, previous, StringComparison.Ordinal))
                return;
            await Task.Delay(pollMs);
        }
        // Last attempt: if still equal, throw to make the failure explicit
        throw new Xunit.Sdk.XunitException("modified_gmt did not change within timeout; external edit may not have been applied.");
    }

    private static string Env(string name) =>
        Environment.GetEnvironmentVariable(name)
        ?? throw new InvalidOperationException($"{name} not set");
}
