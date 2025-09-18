// WpDI/tests/Editor.Tests/DeleteWorkflowTests.cs
using System.Net;
using System.Text;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices; // for [EnumeratorCancellation]
using Editor.Abstractions;
using Editor.WordPress;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class DeleteWorkflowTests
{
    // Local fake stream (separate from the other test to keep files self-contained)
    private sealed class FakeStream : IContentStream
    {
        private readonly ConcurrentDictionary<string, List<PostSummary>> _state = new(StringComparer.Ordinal);

        public void Seed(string restBase, IEnumerable<PostSummary> items)
            => _state[restBase] = new List<PostSummary>(items);

        public void Remove(string restBase, long id)
        {
            if (_state.TryGetValue(restBase, out var list))
                list.RemoveAll(p => p.Id == id);
        }

        public async IAsyncEnumerable<IReadOnlyList<PostSummary>> StreamAllCachedThenFreshAsync(
            string restBase,
            StreamOptions? options = null,
            IProgress<StreamProgress>? progress = null,
            [EnumeratorCancellation] CancellationToken ct = default)
        {
            var list = _state.TryGetValue(restBase, out var items) ? items : new List<PostSummary>();
            yield return list.ToList();
            await Task.CompletedTask;
        }
    }

    private sealed class CapturingHandler : HttpMessageHandler
    {
        public readonly List<(HttpMethod Method, Uri Uri)> Requests = new();
        private readonly Func<HttpRequestMessage, HttpResponseMessage> _responder;

        public CapturingHandler(Func<HttpRequestMessage, HttpResponseMessage> responder) => _responder = responder;

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken ct)
        {
            Requests.Add((request.Method, request.RequestUri!));
            return Task.FromResult(_responder(request));
        }
    }

    private static WordPressApiService NewApi(HttpMessageHandler handler)
    {
        var opts = Options.Create(new WordPressOptions
        {
            BaseUrl = "https://example.test",
            Timeout = TimeSpan.FromSeconds(5)
        });
        var api = new WordPressApiService(opts, () => handler);
        api.SetEndpoint("https://example.test");
        return api;
    }

    [Fact]
    public async Task Delete_Path_Emits_Snapshot_Without_Id()
    {
        const string RestBase = "posts";
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
        var id = 888L;

        var stream = new FakeStream();
        stream.Seed(RestBase, new[]
        {
            new PostSummary(id, "to delete", "draft", "https://example/del", now),
        });

        var feed = new PostFeed(stream, new StreamOptions());
        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var sub = feed.Subscribe(RestBase, cts.Token);

        // Warm
        await feed.RefreshAsync(RestBase, cts.Token);
        var first = await NextSnapshotOrTimeoutAsync(sub, TimeSpan.FromSeconds(5), cts.Token);
        Assert.Contains(first, p => p.Id == id);

        // Editor with OK delete
        var handler = new CapturingHandler(req =>
        {
            if (req.Method == HttpMethod.Delete && req.RequestUri!.AbsolutePath == $"/wp-json/wp/v2/posts/{id}")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var api = NewApi(handler);
        var editor = new WordPressEditor(api);

        await editor.DeleteAsync(id, force: true);

        // Optimistic eviction then refresh
        feed.Evict(RestBase, id);
        stream.Remove(RestBase, id);
        await feed.RefreshAsync(RestBase, cts.Token);

        var second = await NextSnapshotOrTimeoutAsync(sub, TimeSpan.FromSeconds(5), cts.Token);
        Assert.DoesNotContain(second, p => p.Id == id);
        Assert.DoesNotContain(feed.Current(RestBase), p => p.Id == id);

        Assert.Contains(handler.Requests, r =>
            r.Method == HttpMethod.Delete &&
            r.Uri.AbsolutePath == $"/wp-json/wp/v2/posts/{id}" &&
            r.Uri.Query.Contains("force=true", StringComparison.OrdinalIgnoreCase));
    }

    private static async Task<IReadOnlyList<PostSummary>> NextSnapshotOrTimeoutAsync(
        IAsyncEnumerable<IReadOnlyList<PostSummary>> stream,
        TimeSpan timeout,
        CancellationToken ct = default)
    {
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        cts.CancelAfter(timeout);
        await foreach (var snap in stream.WithCancellation(cts.Token))
            return snap;
        throw new TimeoutException("No snapshot within timeout.");
    }
}
