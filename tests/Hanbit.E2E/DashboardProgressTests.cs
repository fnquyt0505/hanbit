using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>T5.4: dashboard + StreakBadge đọc tiến độ thật (quiz Hangul → XP, streak ngày 1 — PRD §6 luồng 1).</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class DashboardProgressTests(WebServerFixture server)
{
    private async Task CompleteHangulQuizAsync(IPage page)
    {
        await page.GotoAsync($"{server.BaseUrl}/hangul/phu-am-kep");
        await page.GetByRole(AriaRole.Button, new() { Name = "kiểm tra ngay" }).ClickAsync();
        await page.GetByTestId("quiz").WaitForAsync();
        for (var i = 1; i <= 10; i++)
        {
            await page.WaitForFunctionAsync("n => document.querySelector('[data-testid=quiz-number]')?.textContent === String(n)", i);
            await page.GetByTestId("quiz-choices").Locator("button").First.ClickAsync();
            await page.GetByTestId("quiz-feedback").WaitForAsync();
            await page.GetByTestId("quiz-continue").ClickAsync();
        }

        await page.GetByTestId("quiz-xp").WaitForAsync();
    }

    [Fact]
    public async Task HangulQuiz_GivesXp_ThenDashboardShowsStreakAndNextLesson()
    {
        var page = await server.NewPageAsync(viewport: new ViewportSize { Width = 1280, Height = 800 });
        await page.AddInitScriptAsync(LessonRunnerTests.FakeSpeechScript);
        await CompleteHangulQuizAsync(page);

        (await page.GetByTestId("quiz-xp").InnerTextAsync()).ShouldMatch(@"^\+(1[0-5]|[5-9]) XP$");
        (await page.EvaluateAsync<string>("() => localStorage.getItem('hanbit.progress.v1')")).ShouldContain("\"hangul/phu-am-kep\"");

        await page.GotoAsync($"{server.BaseUrl}/dashboard");
        await page.WaitForFunctionAsync("() => document.querySelector('[data-testid=dashboard]').getAttribute('aria-busy') === 'false'");

        (await page.GetByTestId("streak-days").InnerTextAsync()).ShouldBe("1");
        int.Parse(await page.GetByTestId("stat-xp").Locator("p[x-text]").InnerTextAsync(), System.Globalization.CultureInfo.InvariantCulture)
            .ShouldBeGreaterThanOrEqualTo(5);
        (await page.GetByTestId("continue-cta").GetAttributeAsync("href")).ShouldBe("/learn/beginner/chao-hoi");

        var navBadge = page.GetByTestId("sidebar").Locator("[data-streak-badge]");
        await navBadge.WaitForAsync();
        (await navBadge.InnerTextAsync()).Trim().ShouldBe("1");
    }

    [Fact]
    public async Task NewLearner_NavBadgeShowsZero_DashboardSuggestsHangul()
    {
        var page = await server.NewPageAsync(viewport: new ViewportSize { Width = 390, Height = 844 });
        await page.GotoAsync($"{server.BaseUrl}/dashboard");

        var badge = page.GetByTestId("mobile-header").Locator("[data-streak-badge]");
        await badge.WaitForAsync();
        (await badge.InnerTextAsync()).Trim().ShouldBe("0");
        (await page.GetByTestId("continue-cta").GetAttributeAsync("href")).ShouldBe("/hangul");
    }
}
