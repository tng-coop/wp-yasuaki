using System.Net.Http.Json;
using Editor.Abstractions;
using Editor.WordPress;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

namespace Editor.Tests;

[Collection("WP EndToEnd")]
public class EditingFacadeCreatePublishE2eTests
{
    private readonly WordPressCleanupFixture _fx;

    public EditingFacadeCreatePublishE2eTests(WordPressCleanupFixture fx) => _fx = fx;

    private IServiceProvider BuildProvider()
    {
        var baseUrl = Env("WP_BASE_URL");
        var user    = Env("WP_USERNAME");
        var pass    = Env("WP_APP_PASSWORD");

        var services = new ServiceCollection();

        // WordPressApiService (admin app-pass)
        services.Configure<WordPressOptions>(o =>
        {
            o.BaseUrl     = baseUrl;
            o.UserName    = user;
            o.AppPassword = pass;
            o.Timeout     = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<IWordPressApiService, WordPressApiService>();

        // Caching + editor services
        services.AddSingleton<IPostCache, MemoryPostCache>();
        services.AddWordPressEditing(); // IPostEditor

        // Locks based on WP HttpClient
        services.AddWpdiEditLocks(sp =>
        {
            var api = sp.GetRequiredService<IWordPressApiService>();
            return api.HttpClient ?? throw new InvalidOperationException("WP HttpClient not initialized.");
        });

        // UI façade under test
        services.AddScoped<IEditingService>(sp =>
            new WordPressEditingService(
                sp.GetRequiredService<IWordPressApiService>(),
                sp.GetRequiredService<IPostEditor>(),
                sp.GetRequiredService<IEditLockService>()));

        return services.BuildServiceProvider();

        static string Env(string name) =>
            Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"{name} not set");
    }

    [Fact]
    public async Task Publish_NewPost_Id0_CreatesAndPublishes()
    {
        using var scope = BuildProvider().CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEditingService>();
        var api = scope.ServiceProvider.GetRequiredService<IWordPressApiService>();

        // simulate the UI: new article has Id = 0
        var post = new Editor.WordPress.PostDetail(
            Id: 0,
            Title: $"facade-create-publish-{Guid.NewGuid():N}",
            Html: "<p>hello</p>",
            Status: "draft",
            CategoryIds: new List<int>(),
            ModifiedUtc: null,
            Link: null);

        // Act: Publish straight from Id=0
        var res = await svc.PublishAsync(post);

        // Assert: façade must create + publish and return real PostId
        Assert.True(res.PostId > 0);
        Assert.Equal(SaveOutcome.Published, res.Outcome);

        // Cleanup
        _fx.RegisterPost(res.PostId);
        var http = api.HttpClient!;
        await http.PostAsJsonAsync($"/wp-json/wp/v2/posts/{res.PostId}", new { status = "draft" });
        // (The fixture will force-delete after run)
    }

    [Fact]
    public async Task SaveDraft_NewPost_Id0_CreatesDraft()
    {
        using var scope = BuildProvider().CreateScope();
        var svc = scope.ServiceProvider.GetRequiredService<IEditingService>();
        var api = scope.ServiceProvider.GetRequiredService<IWordPressApiService>();

        var post = new Editor.WordPress.PostDetail(
            Id: 0,
            Title: $"facade-create-draft-{Guid.NewGuid():N}",
            Html: "<p>draft</p>",
            Status: "draft",
            CategoryIds: new List<int>(),
            ModifiedUtc: null,
            Link: null);

        var res = await svc.SaveDraftAsync(post);

        Assert.True(res.PostId > 0);
        Assert.Contains(res.Outcome, new[] { SaveOutcome.Created, SaveOutcome.Updated });

        _fx.RegisterPost(res.PostId);
        // no further action; fixture will delete
    }
}
