// WpDI/tests/Editor.Tests/WpdiTrickyCasesTests.cs
using System.Net;
using System.Text;
using System.Text.Json;
using Editor.Abstractions;
using Editor.WordPress;
using Microsoft.Extensions.Options;
using Xunit;

public class WpdiTrickyCasesTests
{
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public readonly List<(HttpMethod method, Uri uri, string? body)> Requests = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.Method, request.RequestUri!, body));

            var json = "{\"id\": 123, \"link\": \"/p/123\", \"status\": \"draft\", \"modified_gmt\":\"2024-01-01T00:00:00\"}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    private sealed class CapturingHandlerOverride : HttpMessageHandler
    {
        private readonly string _preGetJson;
        private readonly string _postJson;
        public readonly List<(HttpMethod method, Uri uri, string? body)> Requests = new();

        public CapturingHandlerOverride(string preGetJson, string postJson)
        {
            _preGetJson = preGetJson;
            _postJson = postJson;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.Method, request.RequestUri!, body));

            if (request.Method == HttpMethod.Get && request.RequestUri!.AbsolutePath.StartsWith("/wp-json/wp/v2/posts/"))
            {
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent(_preGetJson, Encoding.UTF8, "application/json")
                };
            }

            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(_postJson, Encoding.UTF8, "application/json")
            };
        }
    }

    private static WordPressApiService NewApi(HttpMessageHandler handler)
    {
        var opts = Options.Create(new WordPressOptions
        {
            BaseUrl = "https://example.test",
            Timeout = TimeSpan.FromSeconds(10)
        });

        var api = new WordPressApiService(opts, () => handler);
        api.SetEndpoint("https://example.test");
        return api;
    }

    [Fact]
    public async Task CreateAsync_Writes_Draft_Post_With_Title_And_Content()
    {
        var handler = new CapturingHandler();
        var api = NewApi(handler);
        var editor = new WordPressEditor(api);

        var result = await editor.CreateAsync("hello", "<p>world</p>");

        Assert.Equal(123, result.Id);
        Assert.Equal("/p/123", result.Url);
        Assert.Equal("draft", result.Status);

        var postReq = handler.Requests.Single(r =>
            r.method == HttpMethod.Post && r.uri.AbsolutePath == "/wp-json/wp/v2/posts");

        var body = postReq.body ?? "";
        using var doc = JsonDocument.Parse(body);
        var root = doc.RootElement;
        Assert.Equal("hello", root.GetProperty("title").GetString());
        var content = root.GetProperty("content").GetString() ?? "";
        Assert.Equal("<p>world</p>", content);
    }

    [Fact]
    public async Task UpdateAsync_Adds_Conflict_Warning_When_Server_Modified_Differs()
    {
        var handler = new CapturingHandlerOverride(
            preGetJson: "{\"id\":123,\"modified_gmt\":\"2025-01-02T03:04:05\"}",
            postJson:   "{\"id\":123,\"link\":\"/p/123\",\"status\":\"draft\"}"
        );
        var api = NewApi(handler);
        var editor = new WordPressEditor(api);

        var _ = await editor.UpdateAsync(123, "<p>new</p>", lastSeenModifiedUtc: "2025-01-01T00:00:00");

        var post = handler.Requests.Last();
        Assert.Equal(HttpMethod.Post, post.method);
        Assert.Equal("/wp-json/wp/v2/posts/123", post.uri.AbsolutePath);
        Assert.Contains("\"wpdi_info\"", post.body);
        Assert.Contains("\"Conflict\"", post.body);
        Assert.Contains("\"baseModifiedUtc\":\"2025-01-01T00:00:00\"", post.body);
        Assert.Contains("\"serverModifiedUtc\":\"2025-01-02T03:04:05\"", post.body);
    }
}
