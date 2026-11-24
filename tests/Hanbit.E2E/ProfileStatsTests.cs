using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>T7.1: /profile — từ đã thuộc/đang học, lịch streak, lọc danh sách từ.</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class ProfileStatsTests(WebServerFixture server)
{
    private const string Seed = """
        () => {
          const now = Date.now();
          const iso = (ms) => new Date(ms).toISOString();
          const day = (offset) => { const d = new Date(now + offset * 86400000 + 7 * 3600000); return d.toISOString().slice(0, 10); };
          const card = (id, interval) => ({ itemId: id, itemType: 'vocab', ease: 2.5, intervalDays: interval, repetitions: 3,
            dueAt: iso(now + interval * 86400000), lastReviewedAt: iso(now) });
          localStorage.setItem('hanbit.progress.v1', JSON.stringify({
            schemaVersion: 1, createdAt: iso(now - 10 * 86400000), lessons: {},
            cards: { 'kd-44487': card('kd-44487', 30), 'kd-17298': card('kd-17298', 3), 'kd-62256': card('kd-62256', 1) },
            activity: [ { date: day(-1), xp: 12, reviews: 3 }, { date: day(0), xp: 60, reviews: 8 } ], totalXp: 72 }));
        }
        """;

    [Fact]
    public async Task Profile_ShowsWordCountsCalendarAndFilters()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/profile");
        await page.EvaluateAsync(Seed);
        await page.ReloadAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('[data-testid=profile-stats]').getAttribute('aria-busy') === 'false'");

        (await page.GetByTestId("stat-mastered").InnerTextAsync()).ShouldBe("1");
        (await page.GetByTestId("stat-learning").InnerTextAsync()).ShouldBe("2");
        (await page.GetByTestId("stat-total-reviews").InnerTextAsync()).ShouldBe("11");
        (await page.GetByTestId("stat-streak-current").InnerTextAsync()).ShouldBe("2");

        var cells = page.GetByTestId("streak-calendar").Locator("[data-level]");
        (await cells.CountAsync()).ShouldBeGreaterThanOrEqualTo(7 * 16 + 1);
        (await cells.Last.GetAttributeAsync("data-level")).ShouldBe("4");

        (await page.GetByTestId("word-list").Locator("li").CountAsync()).ShouldBe(3);
        await page.Locator("[data-filter=mastered]").ClickAsync();
        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-testid=word-list] li').length === 1");
        (await page.GetByTestId("word-list").InnerTextAsync()).ShouldContain("가족");
    }

    [Fact]
    public async Task Mobile_Profile_CalendarScrollsInsideBox_NoPageScroll()
    {
        var page = await server.NewPageAsync(viewport: new ViewportSize { Width = 320, Height = 700 });
        await page.GotoAsync($"{server.BaseUrl}/profile");
        await page.GetByTestId("streak-calendar").WaitForAsync();

        var overflow = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");

        overflow.ShouldBeFalse();
    }
}
