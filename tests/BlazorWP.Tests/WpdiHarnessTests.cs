using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Editor.Abstractions;

namespace BlazorWP.Tests;

public class WpdiHarnessTests : TestContext
{
    public WpdiHarnessTests()
    {
        Services.AddSingleton<IPostEditor, MemoryPostEditor>();
        Services.AddSingleton<IPostFeed, MemoryPostFeed>();
        Services.AddSingleton<Editor.WordPress.IWordPressApiService, FakeApiService>(); // uses the one from Fakes.cs
    }

    [Fact]
    public void Create_List_Delete_Removes_Row()
    {
        var cut = RenderComponent<BlazorWP.Pages.WpdiHarness>();
        var title = $"Unit-{Guid.NewGuid():N}";

        cut.Find("[data-testid='title-input']").Change(title);
        cut.Find("[data-testid='btn-create']").Click();

        cut.Find("[data-testid='btn-list']").Click();
        cut.WaitForAssertion(() =>
        {
            var table = cut.Find("[data-testid='post-table']");
            Assert.Contains(title, table.TextContent);
        });

        cut.Find("[data-testid='btn-delete']").Click();
        cut.WaitForAssertion(() =>
            Assert.Contains("Deleted", cut.Find("[data-testid='status']").TextContent));

        cut.WaitForAssertion(() =>
        {
            var table = cut.Find("[data-testid='post-table']");
            Assert.DoesNotContain(title, table.TextContent);
        });
    }
}

// ===== minimal in-memory WPDI fakes =====
internal sealed class MemoryPostEditor : IPostEditor
{
    internal static readonly Dictionary<long, (string Title, string Status, DateTimeOffset ModifiedGmt)> Store = new();
    private static long _id;
    public Task<EditResult> CreateAsync(string title, string html, CancellationToken ct = default)
    {
        var id = Interlocked.Increment(ref _id);
        Store[id] = (title, "draft", DateTimeOffset.UtcNow);
        return Task.FromResult(new EditResult { Id = id });
    }
    public Task<EditResult> UpdateAsync(long id, string html, string lastSeenModifiedUtc, CancellationToken ct = default)
    {
        if (Store.TryGetValue(id, out var v)) Store[id] = (v.Title, v.Status, DateTimeOffset.UtcNow);
        return Task.FromResult(new EditResult { Id = id });
    }
    public Task<string?> GetLastModifiedUtcAsync(long id, CancellationToken ct = default)
        => Task.FromResult(Store.TryGetValue(id, out var v) ? v.ModifiedGmt.UtcDateTime.ToString("O") : null);
    public Task<EditResult> SetStatusAsync(long id, string status, CancellationToken ct = default)
    {
        if (Store.TryGetValue(id, out var v)) Store[id] = (v.Title, status, DateTimeOffset.UtcNow);
        return Task.FromResult(new EditResult { Id = id });
    }
    public Task DeleteAsync(long id, bool force = true, CancellationToken ct = default)
    { Store.Remove(id); return Task.CompletedTask; }
}

internal sealed class MemoryPostFeed : IPostFeed
{
    public Task RefreshAsync(string restBase, CancellationToken ct = default) => Task.CompletedTask;

    public IReadOnlyList<PostSummary> Current(string restBase)
        => MemoryPostEditor.Store.Select(kv =>
            new PostSummary(kv.Key, kv.Value.Title, kv.Value.Status, "", kv.Value.ModifiedGmt.ToString("O")))
            .OrderByDescending(p => p.ModifiedGmt).ToList();

public IAsyncEnumerable<IReadOnlyList<PostSummary>> Subscribe(string restBase, CancellationToken ct = default)
    => EmptyStream(ct);

private static async IAsyncEnumerable<IReadOnlyList<PostSummary>> EmptyStream(
    [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
{
    // keep an await so the compiler knows this is a real async iterator
    await Task.CompletedTask;
    yield break;
}
    public void Evict(string restBase, long id) => MemoryPostEditor.Store.Remove(id);
    public void Invalidate(string restBase) { }
}
