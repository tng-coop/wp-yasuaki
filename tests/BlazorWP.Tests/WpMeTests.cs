using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using Editor.WordPress;

namespace BlazorWP.Tests;

public class WpMeTests : TestContext
{
    public WpMeTests()
    {
        Services.AddSingleton<IWordPressApiService, FakeApiService>();
        Services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build());
    }

    [Fact]
    public void RendersError_When_ClientIsNull()
    {
        var api = (FakeApiService)Services.GetRequiredService<IWordPressApiService>();
        api._returnNullClient = true;

        var cut = RenderComponent<BlazorWP.Pages.WpMe>();

        cut.WaitForAssertion(() =>
        {
            var err = cut.Find("[data-testid='wp-me-error']");
            Assert.Contains("No WordPress endpoint configured", err.TextContent);
        });
    }

    [Fact]
    public void RendersOk_With_CurrentUser()
    {
        var cut = RenderComponent<BlazorWP.Pages.WpMe>();

        cut.WaitForAssertion(() =>
        {
            var ok = cut.Find("[data-testid='wp-me-ok']");
            Assert.Contains("id:", ok.TextContent);
            Assert.Contains("name:", ok.TextContent);
            Assert.Contains("123", ok.TextContent);
            Assert.Contains("Alice Test", ok.TextContent);
        });
    }
}
