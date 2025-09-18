using System.Threading.Tasks;
using Xunit;
using BlazorWP.Data;

namespace BlazorWP.Tests;

public class LocalStoreTests
{
    [Fact]
    public async Task Add_Todo_Saves_To_LocalStore()
    {
        ILocalStore store = new InMemoryLocalStore();
        await store.AddAsync("todos", new Todo { Title = "Write tests" });

        var all = await store.GetAllAsync<Todo>("todos");
        Assert.Contains(all, t => t.Title == "Write tests");
    }

    [Fact]
    public async Task GetByKey_Returns_Item_By_Id()
    {
        ILocalStore store = new InMemoryLocalStore();

        // Add with Id so the natural key is used
        await store.AddAsync("posts", new Post { Id = 42, Title = "v1" });

        var one = await store.GetByKeyAsync<Post>("posts", 42);
        Assert.NotNull(one);
        Assert.Equal(42, one!.Id);
        Assert.Equal("v1", one.Title);
    }

    [Fact]
    public async Task Put_Upserts_By_Id()
    {
        ILocalStore store = new InMemoryLocalStore();

        await store.AddAsync("posts", new Post { Id = 7, Title = "first" });

        // Upsert same Id
        await store.PutAsync("posts", new Post { Id = 7, Title = "updated" });

        var all = await store.GetAllAsync<Post>("posts");
        Assert.Single(all);                     // no duplicate
        Assert.Equal("updated", all[0].Title);  // value updated
    }

    [Fact]
    public async Task Delete_Removes_By_Key()
    {
        ILocalStore store = new InMemoryLocalStore();

        await store.AddAsync("posts", new Post { Id = 100, Title = "temp" });

        await store.DeleteAsync("posts", 100);

        var one = await store.GetByKeyAsync<Post>("posts", 100);
        Assert.Null(one);
    }

    [Fact]
    public async Task Add_Without_Id_Is_Listable_Even_If_Key_Is_Unknown()
    {
        ILocalStore store = new InMemoryLocalStore();

        // No Id property → AddAsync will generate a key; we can still list it
        await store.AddAsync("misc", new NoId { Name = "keyless" });

        var all = await store.GetAllAsync<NoId>("misc");
        Assert.Single(all);
        Assert.Equal("keyless", all[0].Name);
    }

    private class Todo
    {
        public string Title { get; set; } = "";
    }

    private class Post
    {
        public int Id { get; set; }
        public string Title { get; set; } = "";
    }

    private class NoId
    {
        public string Name { get; set; } = "";
    }
}
