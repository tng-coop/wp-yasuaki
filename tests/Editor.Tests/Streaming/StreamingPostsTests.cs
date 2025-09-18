// WpDI/tests/Editor.Tests/Streaming/StreamingPostsTests.cs
using Editor.Abstractions;
using Editor.WordPress;
using Xunit;
using Microsoft.Extensions.DependencyInjection;


[Collection("WP EndToEnd")]
public class StreamingPostsTests
{
    [Fact]
    public async Task Refresh_EmitsSnapshots_AndContainsNewPosts()
    {
        var (sp, api) = StreamingTestHost.Build();
        var http = api.HttpClient!;
        var feed = sp.GetRequiredService<IPostFeed>();

        // create a few drafts so warm + bulk have something to show
        var ids = new List<long>();
        for (int i = 0; i < 12; i++)
            ids.Add(await StreamingTestHelpers.CreatePostAsync(http, $"stream test {Guid.NewGuid():N}", "<p>hi</p>", "draft"));

        try
        {
            using var subCts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var stream = feed.Subscribe("posts", subCts.Token);

            // kick a refresh (safe to call multiple times)
            await feed.RefreshAsync("posts", subCts.Token);

            // expect at least one snapshot (warm 10), then a later snapshot that includes our ids
            var snap1 = await StreamingTestHelpers.NextSnapshotOrTimeoutAsync(stream, TimeSpan.FromSeconds(10), subCts.Token);
            Assert.True(snap1.Count >= 1);

            // wait for a later snapshot (give crawl time)
            var snap2 = await StreamingTestHelpers.NextSnapshotOrTimeoutAsync(stream, TimeSpan.FromSeconds(10), subCts.Token);

            // verify our created posts appear by Id
            var have = snap2.Select(p => p.Id).ToHashSet();
            foreach (var id in ids)
                Assert.Contains(id, have);
        }
        finally
        {
            foreach (var id in ids) await StreamingTestHelpers.DeletePostHardAsync(http, id);
        }
    }

    [Fact]
    public async Task MultipleSubscribers_ReceiveSnapshots()
    {
        var (sp, api) = StreamingTestHost.Build();
        var http = api.HttpClient!;
        var feed = sp.GetRequiredService<IPostFeed>();

        var id = await StreamingTestHelpers.CreatePostAsync(http, $"stream multi {Guid.NewGuid():N}", "<p>z</p>", "draft");
        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var a = feed.Subscribe("posts", cts.Token);
            var b = feed.Subscribe("posts", cts.Token);

            await feed.RefreshAsync("posts", cts.Token);

            var snapA = await StreamingTestHelpers.NextSnapshotOrTimeoutAsync(a, TimeSpan.FromSeconds(10), cts.Token);
            var snapB = await StreamingTestHelpers.NextSnapshotOrTimeoutAsync(b, TimeSpan.FromSeconds(10), cts.Token);

            Assert.Contains(id, snapA.Select(p => p.Id));
            Assert.Contains(id, snapB.Select(p => p.Id));
        }
        finally
        {
            await StreamingTestHelpers.DeletePostHardAsync(http, id);
        }
    }
}
