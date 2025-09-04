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

    private class Todo
    {
        public string Title { get; set; } = "";
    }
}
