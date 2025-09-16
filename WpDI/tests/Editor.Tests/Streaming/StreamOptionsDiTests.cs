using System.Net;
using System.Text.Json;
using Editor.Abstractions;
using Editor.WordPress;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

public class StreamOptionsDiTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public readonly List<Uri> Requests = new();

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add(request.RequestUri!);

            var query = System.Web.HttpUtility.ParseQueryString(request.RequestUri!.Query);
            int perPage = int.TryParse(query.Get("per_page"), out var n) ? n : 1;
            int count = Math.Min(perPage, 3);

            var json = "[" + string.Join(',', Enumerable.Range(1, count).Select(i => FakePostJson(i))) + "]";
            var resp = new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };
            return Task.FromResult(resp);

            static string FakePostJson(int i) =>
                $"{{\"id\":{i},\"title\":{{\"rendered\":\"p{i}\"}},\"status\":\"draft\",\"link\":\"/p/{i}\",\"modified_gmt\":\"2024-01-01T00:00:00\"}}";
        }
    }

    private sealed class FakeApi : IWordPressApiService
    {
        public HttpClient? HttpClient { get; }
        public WordPressPCL.WordPressClient? Client => null;

        public FakeApi(HttpClient http) { HttpClient = http; }

        public void SetEndpoint(string endpoint) { /* not needed */ }

        public Task<WordPressPCL.WordPressClient?> GetClientAsync()
        {
            // Just return a dummy; tests only care about HttpClient
            return Task.FromResult<WordPressPCL.WordPressClient?>(null);
        }
    }

    [Fact]
    public async Task PostFeed_Uses_StreamOptions_From_DI()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPostCache, MemoryPostCache>();

        var handler = new CapturingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        services.AddSingleton<IWordPressApiService>(sp => new FakeApi(http));
        services.AddWpdiStreaming(configure: () => new StreamOptions(WarmFirstCount: 5, MaxBatchSize: 50));

        var sp = services.BuildServiceProvider();
        var feed = sp.GetRequiredService<IPostFeed>();

        await feed.RefreshAsync("posts");

        Assert.Contains(handler.Requests, u => u.Query.Contains("per_page=5"));
        Assert.Contains(handler.Requests, u => u.Query.Contains("per_page=50"));
    }

    [Fact]
    public async Task PostFeed_Coerces_Invalid_Options_To_Defaults()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPostCache, MemoryPostCache>();

        var handler = new CapturingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        services.AddSingleton<IWordPressApiService>(sp => new FakeApi(http));
        services.AddWpdiStreaming(configure: () => new StreamOptions(WarmFirstCount: 0, MaxBatchSize: -1));

        var sp = services.BuildServiceProvider();
        var feed = sp.GetRequiredService<IPostFeed>();

        await feed.RefreshAsync("posts");

        Assert.Contains(handler.Requests, u => u.Query.Contains("per_page=10"));
        Assert.Contains(handler.Requests, u => u.Query.Contains("per_page=100"));
    }

    [Fact]
    public async Task PostFeed_Uses_Defaults_When_DI_Not_Configured()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IPostCache, MemoryPostCache>();

        var handler = new CapturingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };
        services.AddSingleton<IWordPressApiService>(sp => new FakeApi(http));
        services.AddWpdiStreaming(); // no options supplied

        var sp = services.BuildServiceProvider();
        var feed = sp.GetRequiredService<IPostFeed>();

        await feed.RefreshAsync("posts");

        Assert.Contains(handler.Requests, u => u.Query.Contains("per_page=10"));
        Assert.Contains(handler.Requests, u => u.Query.Contains("per_page=100"));
    }
}
