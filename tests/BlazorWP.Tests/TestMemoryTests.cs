using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using BlazorWP.Data;

namespace BlazorWP.Tests;

public class TestMemoryTests : TestContext
{
    public TestMemoryTests()
    {
        Services.AddSingleton<ILocalStore, MemoryLocalStore>();
    }

    [Fact]
    public void Add_Then_List_Shows_Item()
    {
        var cut = RenderComponent<BlazorWP.Pages.TestMemory>();
        cut.Find("[data-testid='title-input']").Change("Hello");
        cut.Find("[data-testid='btn-add']").Click();
        cut.Find("[data-testid='btn-list']").Click();

        cut.WaitForAssertion(() =>
        {
            var status = cut.Find("[data-testid='status']").TextContent;
            Assert.Contains("Listed", status);
            Assert.NotEmpty(cut.FindAll("[data-testid='draft-item']"));
        });
    }

    [Fact]
    public void Put_Get_Delete_Flow_Works()
    {
        var cut = RenderComponent<BlazorWP.Pages.TestMemory>();

        cut.Find("[data-testid='title-input']").Change("X");
        cut.Find("[data-testid='btn-put']").Click();      // upsert draft:1
        cut.Find("[data-testid='btn-get']").Click();      // read draft:1
        cut.WaitForAssertion(() =>
            Assert.Equal("X", cut.Find("[data-testid='status']").TextContent));

        cut.Find("[data-testid='btn-delete']").Click();
        cut.Find("[data-testid='btn-get']").Click();
        cut.WaitForAssertion(() =>
            Assert.Equal("Not found", cut.Find("[data-testid='status']").TextContent));
    }
}

// In-memory LocalStore for buttons in TestMemory.razor
internal sealed class MemoryLocalStore : ILocalStore
{
    private readonly Dictionary<string, object> _kv = new();
    public Task InitializeAsync() => Task.CompletedTask;

    public Task<T?> GetByKeyAsync<T>(string store, object key)
        => Task.FromResult(_kv.TryGetValue($"{store}:{key}", out var v) ? (T?)v : default);

    public Task<IReadOnlyList<T>> GetAllAsync<T>(string store)
        => Task.FromResult<IReadOnlyList<T>>(_kv.Where(k => k.Key.StartsWith($"{store}:")).Select(k => (T)k.Value).ToList());

    public Task AddAsync<T>(string store, T item) { Upsert(store, item!); return Task.CompletedTask; }
    public Task PutAsync<T>(string store, T item) { Upsert(store, item!); return Task.CompletedTask; }
    public Task DeleteAsync(string store, object key) { _kv.Remove($"{store}:{key}"); return Task.CompletedTask; }

    private static readonly string[] Keys = { "id", "Id", "ID", "Key", "key" };
    private void Upsert<T>(string store, T item)
    {
        var id = item!.GetType().GetProperties().FirstOrDefault(p => Keys.Contains(p.Name))?.GetValue(item)?.ToString();
        if (string.IsNullOrEmpty(id)) id = Guid.NewGuid().ToString("N");
        _kv[$"{store}:{id}"] = item!;
    }
}
