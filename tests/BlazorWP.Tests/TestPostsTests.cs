using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Editor.Abstractions;

namespace BlazorWP.Tests;

public class TestPostsTests : TestContext
{
    public TestPostsTests()
    {
        Services.AddSingleton<IPostEditor, MemoryPostEditor>(); // reuse from Wpdi tests
        Services.AddSingleton<IPostFeed, MemoryPostFeed>();     // reuse
        Services.AddSingleton<Editor.WordPress.IWordPressApiService, FakeApiService>();
    }

    [Fact]
    public void Add_Then_List_Renders_Items_And_Status()
    {
        var cut = RenderComponent<BlazorWP.Pages.TestPosts>();
        var title = "FromUnit";

        cut.Find("[data-testid='title-input']").Change(title);
        cut.Find("[data-testid='btn-add']").Click();
        cut.Find("[data-testid='btn-list']").Click();

        cut.WaitForAssertion(() =>
        {
            Assert.Contains("Listed", cut.Find("[data-testid='status']").TextContent);
            var list = cut.Find("[data-testid='post-list']").TextContent;
            Assert.Contains("FromUnit", list);
        });
    }
}
