using System.Net;
using System.Text;
using System.Text.Json;
using Editor.Abstractions;
using Editor.WordPress;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class WpdiEditorWorkflowTests
{
    // -----------------------------------------------------------------------------
    // Test infrastructure (matches style of WpdiTrickyCasesTests)
    // -----------------------------------------------------------------------------

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public readonly List<(HttpMethod method, Uri uri, string? body, IDictionary<string, string> headers)> Requests = new();

        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder)
        {
            _responder = responder;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            string? body = request.Content is null ? null : await request.Content.ReadAsStringAsync(ct);
            var headers = request.Headers.ToDictionary(h => h.Key, h => string.Join(",", h.Value));
            Requests.Add((request.Method, request.RequestUri!, body, headers));
            return _responder(request);
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

    private static HttpResponseMessage JsonOk(object obj)
        => new(HttpStatusCode.OK) { Content = new StringContent(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json") };

    // -----------------------------------------------------------------------------
    // GetLastModifiedUtcAsync
    // -----------------------------------------------------------------------------

    [Fact]
    public async Task GetLastModifiedUtcAsync_Returns_ModifiedGmt_When_Present()
    {
        var handler = new CapturingHandler(req =>
        {
            if (req.Method == HttpMethod.Get && req.RequestUri!.AbsolutePath.StartsWith("/wp-json/wp/v2/posts/"))
            {
                return JsonOk(new { id = 42, modified_gmt = "2025-01-02T03:04:05" });
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var api = NewApi(handler);
        var editor = new WordPressEditor(api);

        var value = await editor.GetLastModifiedUtcAsync(42);

        Assert.Equal("2025-01-02T03:04:05", value);
        var get = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Get, get.method);
        Assert.StartsWith("/wp-json/wp/v2/posts/42", get.uri.AbsolutePath);
        Assert.Contains("context=edit", get.uri.Query); // we expect context=edit for drafts
    }

    [Fact]
    public async Task GetLastModifiedUtcAsync_Returns_Null_When_Field_Absent_Or_Non200()
    {
        // Case A: 200 but no modified_gmt
        var handlerA = new CapturingHandler(_ => JsonOk(new { id = 7 }));
        var apiA = NewApi(handlerA);
        var editorA = new WordPressEditor(apiA);
        var valueA = await editorA.GetLastModifiedUtcAsync(7);
        Assert.Null(valueA);

        // Case B: 404
        var handlerB = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        var apiB = NewApi(handlerB);
        var editorB = new WordPressEditor(apiB);
        var valueB = await editorB.GetLastModifiedUtcAsync(7);
        Assert.Null(valueB);
    }

    // -----------------------------------------------------------------------------
    // SetStatusAsync
    // -----------------------------------------------------------------------------

    [Fact]
    public async Task SetStatusAsync_Posts_Status_And_Returns_EditResult()
    {
        var handler = new CapturingHandler(req =>
        {
            if (req.Method == HttpMethod.Post && req.RequestUri!.AbsolutePath == "/wp-json/wp/v2/posts/123")
            {
                // The real endpoint returns id/link/status; mimic that.
                return JsonOk(new { id = 123L, link = "https://example.test/hello-world/", status = "publish" });
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });

        var api = NewApi(handler);
        var editor = new WordPressEditor(api);

        var result = await editor.SetStatusAsync(123, "publish");

        Assert.Equal(123, result.Id);
        Assert.Equal("publish", result.Status);
        Assert.Equal("https://example.test/hello-world/", result.Link);

        var req = Assert.Single(handler.Requests);
        Assert.Equal(HttpMethod.Post, req.method);
        Assert.Equal("/wp-json/wp/v2/posts/123", req.uri.AbsolutePath);
        Assert.Contains("\"status\":\"publish\"", req.body);
    }

    // -----------------------------------------------------------------------------
    // DeleteAsync
    // -----------------------------------------------------------------------------

    [Fact]
    public async Task DeleteAsync_Succeeds_On_200_And_404_And_410()
    {
        // Simulate sequence: 200 OK, 404 NotFound, 410 Gone
        var calls = 0;
        var statuses = new[] { HttpStatusCode.OK, HttpStatusCode.NotFound, HttpStatusCode.Gone };
        var handler = new CapturingHandler(_ =>
        {
            var code = statuses[Math.Min(calls, statuses.Length - 1)];
            calls++;
            return new HttpResponseMessage(code) { Content = new StringContent("") };
        });

        var api = NewApi(handler);
        var editor = new WordPressEditor(api);

        await editor.DeleteAsync(999, force: true); // 200
        await editor.DeleteAsync(999, force: true); // 404
        await editor.DeleteAsync(999, force: true); // 410

        Assert.Equal(3, handler.Requests.Count);
        Assert.All(handler.Requests, r =>
        {
            Assert.Equal(HttpMethod.Delete, r.method);
            Assert.Equal("/wp-json/wp/v2/posts/999", r.uri.AbsolutePath);
            Assert.Contains("force=true", r.uri.Query);
        });
    }

    [Fact]
    public async Task DeleteAsync_Throws_On_Server_Error()
    {
        var handler = new CapturingHandler(_ => new HttpResponseMessage(HttpStatusCode.InternalServerError));
        var api = NewApi(handler);
        var editor = new WordPressEditor(api);

        await Assert.ThrowsAsync<HttpRequestException>(async () =>
        {
            await editor.DeleteAsync(555, force: true);
        });
    }
}
