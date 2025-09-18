// WpDI/tests/Editor.Tests/DeleteInfrastructureTests.cs
using System.Net;
using System.Text;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices; // for [EnumeratorCancellation]
using Editor.Abstractions;
using Editor.WordPress;
using Microsoft.Extensions.Options;
using Xunit;

public sealed class DeleteInfrastructureTests
{
    // ---- Minimal fake IContentStream that emits snapshots from in-memory state ----
    private sealed class FakeStream : IContentStream
    {
        // restBase -> list of posts (server view)
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
    public async Task Delete_EvictsLocally_Notifies_And_RefreshKeepsItGone()
    {
        const string RestBase = "posts";
        var now = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss");
        var keepId = 42L;
        var delId  = 777L;

        // Fake stream starts with two posts
        var stream = new FakeStream();
        stream.Seed(RestBase, new[]
        {
            new PostSummary(keepId, "keep me",   "draft",   "https://example/keep", now),
            new PostSummary(delId,  "delete me", "publish", "https://example/del",  now),
        });

        // Build PostFeed on top of FakeStream
        var feed = new PostFeed(stream, new StreamOptions(WarmFirstCount: 10, MaxBatchSize: 100));

        using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
        var sub = feed.Subscribe(RestBase, cts.Token);

        // Initial refresh → both present
        await feed.RefreshAsync(RestBase, cts.Token);
        var first = await NextSnapshotOrTimeoutAsync(sub, TimeSpan.FromSeconds(5), cts.Token);
        Assert.Contains(first, p => p.Id == keepId);
        Assert.Contains(first, p => p.Id == delId);

        // Editor wired with HTTP that returns 200 OK to DELETE
        var handler = new CapturingHandler(req =>
        {
            if (req.Method == HttpMethod.Delete && req.RequestUri!.AbsolutePath == $"/wp-json/wp/v2/posts/{delId}")
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent("{}", Encoding.UTF8, "application/json") };
            return new HttpResponseMessage(HttpStatusCode.NotFound);
        });
        var api = NewApi(handler);
        var editor = new WordPressEditor(api);

        // Perform server delete (idempotent: 200/404/410 success)
        await editor.DeleteAsync(delId, force: true);

        // Optimistic eviction: immediately drop from local snapshot + notify
        feed.Evict(RestBase, delId);

        // After eviction, subscribers receive a snapshot without the id
        var afterEvict = await NextSnapshotOrTimeoutAsync(sub, TimeSpan.FromSeconds(5), cts.Token);
        Assert.DoesNotContain(afterEvict, p => p.Id == delId);
        Assert.Contains(afterEvict, p => p.Id == keepId);
        Assert.DoesNotContain(feed.Current(RestBase), p => p.Id == delId);

        // Simulate the server reflecting deletion on next crawl
        stream.Remove(RestBase, delId);

        // Standard refresh — still no delId
        await feed.RefreshAsync(RestBase, cts.Token);
        var afterRefresh = await NextSnapshotOrTimeoutAsync(sub, TimeSpan.FromSeconds(5), cts.Token);
        Assert.DoesNotContain(afterRefresh, p => p.Id == delId);

        // And we issued the correct DELETE with force=true
        Assert.Contains(handler.Requests, r =>
            r.Method == HttpMethod.Delete &&
            r.Uri.AbsolutePath == $"/wp-json/wp/v2/posts/{delId}" &&
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
