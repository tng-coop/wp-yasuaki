using System;
using System.Threading;
using Bunit;
using Microsoft.Extensions.DependencyInjection;
using Xunit;
using BlazorWP.Data;
using Microsoft.AspNetCore.Components;
using Microsoft.JSInterop;
using Editor.WordPress; // ✅ for IWordPressApiService

namespace BlazorWP.Tests;

public class AppFlagsTests : TestContext
{
    public AppFlagsTests()
    {
        // AppFlags dependency (no real JS/localStorage)
        Services.AddSingleton<AppFlags, TestAppFlags>();

        // Minimal NavigationManager so the page can navigate if needed
        Services.AddSingleton<NavigationManager>(new TestNavigationManager());

        // JS interop injected by AppFlags.razor
        Services.AddSingleton(new BlazorWP.WpNonceJsInterop(new DummyJsRuntime()));

        // AppFlags also injects this concrete service
        Services.AddSingleton(
            new BlazorWP.AppPasswordService(
                new BlazorWP.LocalStorageJsInterop(new DummyJsRuntime())
            )
        );

        // ✅ AppFlags injects IWordPressApiService (use our shared fake from Fakes.cs)
        Services.AddSingleton<IWordPressApiService, FakeApiService>();

        // ✅ Some components read IConfiguration; safe to provide an empty one
        Services.AddSingleton<Microsoft.Extensions.Configuration.IConfiguration>(
            new Microsoft.Extensions.Configuration.ConfigurationBuilder().Build()
        );
        Services.AddLocalization(options => options.ResourcesPath = "Resources");
    }

    [Fact]
    public void Initial_State_Panel_Renders()
    {
        var cut = RenderComponent<BlazorWP.Pages.AppFlags>();
        cut.Find("[data-testid='state-panel']");
        cut.Find("[data-testid='flags-table']");
    }

    [Fact]
    public void Clicking_Mode_Auth_Lang_Updates_State_Panel()
    {
        var cut = RenderComponent<BlazorWP.Pages.AppFlags>();

        // Mode → Basic
        cut.Find("[data-testid='appmode-basic']").Click();
        cut.WaitForAssertion(() =>
            Assert.Equal("Basic", cut.Find("[data-testid='state-mode']").TextContent.Trim()));

        // Auth → Nonce
        cut.Find("[data-testid='auth-nonce']").Click();
        cut.WaitForAssertion(() =>
            Assert.Equal("Nonce", cut.Find("[data-testid='state-auth']").TextContent.Trim()));

        // Language → Japanese
        cut.Find("[data-testid='lang-japanese']").Click();
        cut.WaitForAssertion(() =>
            Assert.Equal("Japanese", cut.Find("[data-testid='state-lang']").TextContent.Trim()));
    }
}

/* ---------- helpers ---------- */

internal sealed class TestAppFlags : AppFlags
{
    public TestAppFlags() : base(storage: new FakeLocalStorageJs()) { }
}

// We don't override anything — the base class will invoke JS,
// and this runtime just returns default values immediately.
internal sealed class FakeLocalStorageJs : BlazorWP.LocalStorageJsInterop
{
    public FakeLocalStorageJs() : base(new DummyJsRuntime()) { }
}

internal sealed class DummyJsRuntime : IJSRuntime
{
    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, object?[]? args)
        => ValueTask.FromResult<TValue>(default!);

    public ValueTask<TValue> InvokeAsync<TValue>(string identifier, CancellationToken cancellationToken, object?[]? args)
        => ValueTask.FromResult<TValue>(default!);
}

internal sealed class TestNavigationManager : NavigationManager
{
    public TestNavigationManager()
    {
        Initialize("http://localhost/", "http://localhost/appflags");
    }

    protected override void NavigateToCore(string uri, bool forceLoad) { /* no-op */ }
}
