using Deque.AxeCore.Commons;
using Deque.AxeCore.Playwright;

using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>T7.4: axe-core (WCAG 2.x A/AA) trên các trang chính, sáng + tối — không có vi phạm serious/critical.</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class AccessibilityTests(WebServerFixture server)
{
    private static readonly string[] Pages =
    [
        "/", "/dashboard", "/hangul", "/hangul/phu-am", "/learn/beginner", "/learn/beginner/chao-hoi",
        "/review", "/profile", "/nguon-du-lieu", "/trang-chua-co", "/offline",
    ];

    public static TheoryData<string, string> PagesAndThemes()
    {
        var data = new TheoryData<string, string>();
        foreach (var path in Pages)
        {
            data.Add(path, "light");
            data.Add(path, "dark");
        }

        return data;
    }

    private static readonly AxeRunOptions Options = new()
    {
        RunOnly = new RunOnlyOptions { Type = "tag", Values = ["wcag2a", "wcag2aa", "wcag21a", "wcag21aa", "best-practice"] },
    };

    private static async Task ShouldHaveNoSeriousViolationsAsync(IPage page)
    {
        await page.WaitForTimeoutAsync(300);
        var result = await page.RunAxe(Options);
        var serious = result.Violations
            .Where(v => v.Impact is "serious" or "critical")
            .Select(v => $"{v.Id} ({v.Impact}): {v.Help} — {string.Join(" | ", v.Nodes.Take(3).Select(n => string.Join(",", n.Target)))}")
            .ToList();
        serious.ShouldBeEmpty(string.Join("\n", serious));
    }

    [Fact]
    public async Task InteractiveStates_ExerciseQuizAndFlippedCard_HaveNoSeriousViolations()
    {
        var page = await LessonRunnerTests.OpenLessonAsync(server);
        await LessonRunnerTests.SkipToExercisesAsync(page);
        await ShouldHaveNoSeriousViolationsAsync(page); // bài tập chưa trả lời
        await LessonRunnerTests.AnswerCurrentAsync(page);
        await page.GetByTestId("exercise-feedback").WaitForAsync();
        await ShouldHaveNoSeriousViolationsAsync(page); // phản hồi đúng/sai

        await page.GotoAsync($"{server.BaseUrl}/hangul/nguyen-am");
        await page.GetByRole(AriaRole.Button, new() { Name = "kiểm tra ngay" }).ClickAsync();
        await page.GetByTestId("quiz").WaitForAsync();
        await ShouldHaveNoSeriousViolationsAsync(page); // quiz Hangul

        await page.EvaluateAsync("""
            () => { const due = new Date(Date.now() - 60_000).toISOString();
              localStorage.setItem('hanbit.progress.v1', JSON.stringify({ schemaVersion: 1, createdAt: due, lessons: {}, activity: [], totalXp: 0,
                cards: { 'kd-44487': { itemId: 'kd-44487', itemType: 'vocab', ease: 2.5, intervalDays: 0, repetitions: 0, dueAt: due, lastReviewedAt: null } } })); }
            """);
        await page.GotoAsync($"{server.BaseUrl}/review");
        await page.GetByTestId("flip").ClickAsync();
        await page.GetByTestId("grades").WaitForAsync();
        await ShouldHaveNoSeriousViolationsAsync(page); // thẻ đã lật + nút đánh giá
    }

    [Theory]
    [MemberData(nameof(PagesAndThemes))]
    public async Task Page_HasNoSeriousAccessibilityViolations(string path, string theme)
    {
        var page = await server.NewPageAsync(theme == "dark" ? ColorScheme.Dark : ColorScheme.Light);
        await page.GotoAsync($"{server.BaseUrl}{path}");
        await page.WaitForFunctionAsync("() => window.Alpine && window.Alpine.store('progress')?.loaded === true");
        await page.WaitForTimeoutAsync(300); // chờ transition/animation kết thúc

        var result = await page.RunAxe(Options);

        var serious = result.Violations
            .Where(v => v.Impact is "serious" or "critical")
            .Select(v => $"{v.Id} ({v.Impact}): {v.Help} — {string.Join(" | ", v.Nodes.Take(3).Select(n => string.Join(",", n.Target)))}")
            .ToList();
        serious.ShouldBeEmpty(string.Join("\n", serious));
    }
}
