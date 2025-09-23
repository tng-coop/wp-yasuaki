using System.Text.Json;
using Editor.WordPress;
using Microsoft.Extensions.Options;
using Xunit;

namespace Editor.Tests;

public sealed class WordPressCleanupFixture : IAsyncLifetime
{
    private WordPressApiService? _api;
    private readonly List<long> _postIds = new();

    public WordPressApiService Api =>
        _api ?? throw new InvalidOperationException("API not initialized yet.");

    public void RegisterPost(long id)
    {
        if (id > 0) _postIds.Add(id);
    }

    public Task InitializeAsync()
    {
        var baseUrl = Env("WP_BASE_URL");
        var user    = Env("WP_USERNAME");
        var pass    = Env("WP_APP_PASSWORD");

        _api = new WordPressApiService(Options.Create(new WordPressOptions
        {
            BaseUrl     = baseUrl,
            UserName    = user,
            AppPassword = pass,
            Timeout     = TimeSpan.FromSeconds(20)
        }));

        return Task.CompletedTask;

        static string Env(string name) =>
            Environment.GetEnvironmentVariable(name)
            ?? throw new InvalidOperationException($"{name} not set");
    }

    public async Task DisposeAsync()
    {
        if (_api is null) return;
        var http = _api.HttpClient!;
        foreach (var id in _postIds.Distinct())
        {
            try { await http.DeleteAsync($"/wp-json/wp/v2/posts/{id}?force=true"); }
            catch { /* ignore cleanup failures */ }
        }
    }
}
