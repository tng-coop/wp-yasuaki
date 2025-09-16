// WpDI/tests/Editor.Tests/WpdiTrickyCasesTests.cs
using System.Net;
using System.Text;
using System.Text.Json;
using Editor.Abstractions;
using Editor.WordPress;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Xunit;

public class WpdiTrickyCasesTests
{
    // Capturing/fake handler(s) you already use in this test file
    private sealed class CapturingHandler : HttpMessageHandler
    {
        public readonly List<(HttpMethod method, Uri uri, string? body)> Requests = new();

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            Requests.Add((request.Method, request.RequestUri!, body));

            // Return a basic OK with minimal JSON unless overridden in a test
            var json = "{\"id\": 123, \"link\": \"/p/123\", \"status\": \"draft\", \"modified_gmt\":\"2024-01-01T00:00:00\"}";
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }
    }

    /// <summary>
    /// Minimal API wrapper so WordPressEditor can be constructed with IWordPressApiService.
    /// It only needs to expose the HttpClient for these tests.
    /// </summary>
    private sealed class FakeApi : IWordPressApiService
    {
        public HttpClient? HttpClient { get; }
        public WordPressPCL.WordPressClient? Client => null;

        public FakeApi(HttpClient http) => HttpClient = http;

        public void SetEndpoint(string endpoint) { /* not needed for these unit tests */ }

        public Task<WordPressPCL.WordPressClient?> GetClientAsync()
            => Task.FromResult<WordPressPCL.WordPressClient?>(null);
    }

    [Fact]
    public async Task CreateAsync_Writes_Draft_Post_With_Title_And_Content()
    {
        var handler = new CapturingHandler();
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };

        var api = new FakeApi(http);
        var editor = new WordPressEditor(api); // uses API service now

        var result = await editor.CreateAsync("hello", "<p>world</p>");

        Assert.Equal(123, result.Id);
        Assert.Equal("/p/123", result.Url); // Link -> Url
        Assert.Equal("draft", result.Status);

        // ---- FIXED ASSERTION: parse JSON and check decoded content string ----
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
        // Arrange a handler that returns a different modified_gmt on the preflight GET
        var handler = new CapturingHandlerOverride(
            preGetJson: "{\"id\":123,\"modified_gmt\":\"2025-01-02T03:04:05\"}",
            postJson:   "{\"id\":123,\"link\":\"/p/123\",\"status\":\"draft\"}"
        );
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://example.test") };

        var api = new FakeApi(http);
        var editor = new WordPressEditor(api);

        // Act
        var _ = await editor.UpdateAsync(123, "<p>new</p>", lastSeenModifiedUtc: "2025-01-01T00:00:00");

        // Assert: body should include meta.wpdi_info with conflict info
        var post = handler.Requests.Last();
        Assert.Equal(HttpMethod.Post, post.method);
        Assert.Equal("/wp-json/wp/v2/posts/123", post.uri.AbsolutePath);
        Assert.Contains("\"wpdi_info\"", post.body);
        Assert.Contains("\"Conflict\"", post.body); // reason.code
        Assert.Contains("\"baseModifiedUtc\":\"2025-01-01T00:00:00\"", post.body);
        Assert.Contains("\"serverModifiedUtc\":\"2025-01-02T03:04:05\"", post.body);
    }

    // Helper handler that returns custom JSON for preflight GET vs. POST
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
}
