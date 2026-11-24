using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>T2.1: mobile (&lt; 1024px) dùng bottom nav, desktop dùng sidebar; footer ghi công luôn hiện.</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class LayoutTests(WebServerFixture server)
{
    private static readonly ViewportSize Mobile = new() { Width = 390, Height = 844 };
    private static readonly ViewportSize Tablet = new() { Width = 800, Height = 1024 };
    private static readonly ViewportSize Desktop = new() { Width = 1280, Height = 800 };

    public static TheoryData<string> SmallScreens => ["mobile", "tablet"];

    private static ViewportSize Viewport(string name) => name switch
    {
        "mobile" => Mobile,
        "tablet" => Tablet,
        _ => Desktop,
    };

    [Theory]
    [MemberData(nameof(SmallScreens))]
    public async Task SmallScreen_ShowsBottomNav_HidesSidebar(string screen)
    {
        var page = await server.NewPageAsync(viewport: Viewport(screen));
        await page.GotoAsync(server.BaseUrl);

        (await page.GetByTestId("bottom-nav").IsVisibleAsync()).ShouldBeTrue();
        (await page.GetByTestId("sidebar").IsVisibleAsync()).ShouldBeFalse();
        (await page.GetByTestId("mobile-header").IsVisibleAsync()).ShouldBeTrue();
    }

    [Fact]
    public async Task Desktop_ShowsSidebar_HidesBottomNav()
    {
        var page = await server.NewPageAsync(viewport: Desktop);
        await page.GotoAsync(server.BaseUrl);

        (await page.GetByTestId("sidebar").IsVisibleAsync()).ShouldBeTrue();
        (await page.GetByTestId("bottom-nav").IsVisibleAsync()).ShouldBeFalse();
        (await page.GetByTestId("mobile-header").IsVisibleAsync()).ShouldBeFalse();
    }

    [Fact]
    public async Task Mobile_BottomNav_DoesNotCoverFooter()
    {
        var page = await server.NewPageAsync(viewport: Mobile);
        await page.GotoAsync(server.BaseUrl);
        await page.EvaluateAsync("() => window.scrollTo(0, document.body.scrollHeight)");

        var footer = await page.GetByTestId("attribution").BoundingBoxAsync();
        var nav = await page.GetByTestId("bottom-nav").BoundingBoxAsync();

        footer.ShouldNotBeNull();
        nav.ShouldNotBeNull();
        (footer.Y + footer.Height).ShouldBeLessThanOrEqualTo(nav.Y);
    }

    [Fact]
    public async Task Mobile_HasNoHorizontalScroll()
    {
        var page = await server.NewPageAsync(viewport: new ViewportSize { Width = 320, Height = 640 });
        await page.GotoAsync(server.BaseUrl);

        var overflow = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");

        overflow.ShouldBeFalse();
    }

    [Fact]
    public async Task Footer_ShowsKrdictAttribution()
    {
        var page = await server.NewPageAsync(viewport: Desktop);
        await page.GotoAsync(server.BaseUrl);

        var text = await page.GetByTestId("attribution").InnerTextAsync();

        text.ShouldContain("한국어기초사전");
        text.ShouldContain("국립국어원");
        text.ShouldContain("CC BY-SA 2.0 KR");
    }
}
