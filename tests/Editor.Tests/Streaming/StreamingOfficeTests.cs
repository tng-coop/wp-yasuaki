// WpDI/tests/Editor.Tests/Streaming/StreamingOfficeTests.cs
using Editor.Abstractions;
using Editor.WordPress;
using Microsoft.Extensions.DependencyInjection;

using Xunit;

[Collection("WP EndToEnd")]
public class StreamingOfficeTests
{
    private static string OfficeRestBase()
        => Environment.GetEnvironmentVariable("WP_REST_BASE_OFFICE") ?? "office-cpt";

    [Fact]
    public async Task Refresh_EmitsSnapshots_ForOfficeCpt()
    {
        var (sp, api) = StreamingTestHost.Build();
        var http = api.HttpClient!;
        var feed = sp.GetRequiredService<IPostFeed>();
        var restBase = OfficeRestBase();

        // create offices
        var ids = new List<long>();
        for (int i = 0; i < 3; i++)
        {
            var data = new { address = $"addr-{i}", floors = 5 + i };
            ids.Add(await StreamingTestHelpers.CreateOfficeAsync(http, restBase, $"Office {Guid.NewGuid():N}", data, "draft"));
        }

        try
        {
            using var cts = new CancellationTokenSource(TimeSpan.FromSeconds(20));
            var stream = feed.Subscribe(restBase, cts.Token);
            await feed.RefreshAsync(restBase, cts.Token);

            var snap = await StreamingTestHelpers.NextSnapshotOrTimeoutAsync(stream, TimeSpan.FromSeconds(10), cts.Token);
            var have = snap.Select(p => p.Id).ToHashSet();

            foreach (var id in ids)
                Assert.Contains(id, have);
        }
        finally
        {
            foreach (var id in ids) await StreamingTestHelpers.DeleteOfficeHardAsync(http, restBase, id);
        }
    }
}
