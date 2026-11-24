using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>T2.4: skeleton được Alpine thay bằng số liệu (người học mới → 0) và CTA học Hangul.</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class DashboardTests(WebServerFixture server)
{
    [Fact]
    public async Task Open_Dashboard_ReplacesSkeletonWithNewLearnerState()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/dashboard");

        var dashboard = page.GetByTestId("dashboard");
        await page.WaitForFunctionAsync("() => document.querySelector('[data-testid=dashboard]').getAttribute('aria-busy') === 'false'");

        (await page.Locator("[data-skeleton]").First.IsVisibleAsync()).ShouldBeFalse();
        (await page.GetByTestId("streak-days").InnerTextAsync()).ShouldBe("0");
        (await dashboard.GetByTestId("stat-due").InnerTextAsync()).ShouldContain("0");
        (await page.GetByTestId("continue-cta").GetAttributeAsync("href")).ShouldBe("/hangul");
    }

    [Fact]
    public async Task Click_SidebarDashboard_FromHome_OpensDashboard()
    {
        var page = await server.NewPageAsync(viewport: new ViewportSize { Width = 1280, Height = 800 });
        await page.GotoAsync(server.BaseUrl);

        await page.GetByTestId("sidebar").GetByRole(AriaRole.Link, new() { Name = "Tổng quan", Exact = true }).ClickAsync();
        await page.GetByTestId("dashboard").WaitForAsync();

        new Uri(page.Url).AbsolutePath.ShouldBe("/dashboard");
    }

    [Fact]
    public async Task Mobile_Dashboard_HasNoHorizontalScroll()
    {
        var page = await server.NewPageAsync(viewport: new ViewportSize { Width = 320, Height = 640 });
        await page.GotoAsync($"{server.BaseUrl}/dashboard");

        var overflow = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");

        overflow.ShouldBeFalse();
    }
}
