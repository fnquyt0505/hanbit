using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>T6.4: dashboard + điều hướng hiển thị số thẻ đến hạn ("Ôn tập (n thẻ)" — PRD §6 luồng 2).</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class DueCountTests(WebServerFixture server)
{
    private const string SeedTwoDueOneLater = """
        () => {
          const past = new Date(Date.now() - 3600_000).toISOString();
          const future = new Date(Date.now() + 5 * 86400_000).toISOString();
          const card = (id, due) => ({ itemId: id, itemType: 'vocab', ease: 2.5, intervalDays: 1, repetitions: 1, dueAt: due, lastReviewedAt: null });
          localStorage.setItem('hanbit.progress.v1', JSON.stringify({
            schemaVersion: 1, createdAt: new Date().toISOString(),
            lessons: { 'beginner/chao-hoi': { status: 'completed', bestScore: 80, completedAt: past } },
            cards: { 'kd-17298': card('kd-17298', past), 'kd-44487': card('kd-44487', past), 'kd-62256': card('kd-62256', future) },
            activity: [], totalXp: 20 }));
        }
        """;

    private async Task<IPage> OpenSeededDashboardAsync(ViewportSize viewport)
    {
        var page = await server.NewPageAsync(viewport: viewport);
        await page.GotoAsync($"{server.BaseUrl}/profile");
        await page.EvaluateAsync(SeedTwoDueOneLater);
        await page.GotoAsync($"{server.BaseUrl}/dashboard");
        await page.WaitForFunctionAsync("() => document.querySelector('[data-testid=dashboard]').getAttribute('aria-busy') === 'false'");
        return page;
    }

    [Fact]
    public async Task Desktop_DashboardAndSidebarShowDueCount()
    {
        var page = await OpenSeededDashboardAsync(new ViewportSize { Width = 1280, Height = 800 });

        (await page.GetByTestId("stat-due").Locator("p[x-text]").InnerTextAsync()).ShouldBe("2");
        (await page.GetByTestId("review-cta").InnerTextAsync()).ShouldBe("Ôn tập (2 thẻ)");

        var badge = page.GetByTestId("sidebar").Locator("[data-due-badge]");
        await badge.WaitForAsync();
        (await badge.InnerTextAsync()).ShouldBe("2");
        (await badge.GetAttributeAsync("aria-label")).ShouldBe("2 thẻ cần ôn");
    }

    [Fact]
    public async Task Mobile_BottomNavShowsDueCount()
    {
        var page = await OpenSeededDashboardAsync(new ViewportSize { Width = 390, Height = 844 });

        var badge = page.GetByTestId("bottom-nav").Locator("[data-due-badge]");
        await badge.WaitForAsync();
        (await badge.InnerTextAsync()).ShouldBe("2");
    }

    [Fact]
    public async Task NoDueCards_BadgeHidden_ButtonPlain()
    {
        var page = await server.NewPageAsync(viewport: new ViewportSize { Width = 1280, Height = 800 });
        await page.GotoAsync($"{server.BaseUrl}/dashboard");
        await page.WaitForFunctionAsync("() => document.querySelector('[data-testid=dashboard]').getAttribute('aria-busy') === 'false'");

        (await page.GetByTestId("review-cta").InnerTextAsync()).ShouldBe("Ôn tập");
        (await page.GetByTestId("sidebar").Locator("[data-due-badge]").IsVisibleAsync()).ShouldBeFalse();
    }
}
