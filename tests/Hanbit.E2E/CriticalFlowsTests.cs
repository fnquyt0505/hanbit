using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>
/// T7.2 — 3 luồng chính (RULES §8, PRD §6) đi như người dùng thật qua giao diện, kiểm tra cả localStorage:
/// (1) người mới học Hangul bài 1, (2) học một bài học, (3) một phiên ôn tập.
/// </summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class CriticalFlowsTests(WebServerFixture server)
{
    private static readonly ViewportSize Mobile = new() { Width = 390, Height = 844 };
    private static readonly ViewportSize Desktop = new() { Width = 1280, Height = 800 };

    private async Task<IPage> NewPageAsync(ViewportSize viewport)
    {
        var page = await server.NewPageAsync(viewport: viewport);
        await page.AddInitScriptAsync(LessonRunnerTests.FakeSpeechScript);
        return page;
    }

    private static Task<T> StoredAsync<T>(IPage page, string expression) =>
        page.EvaluateAsync<T>($"() => {{ const p = JSON.parse(localStorage.getItem('hanbit.progress.v1')); return {expression}; }}");

    private static Task WaitDashboardLoadedAsync(IPage page) =>
        page.WaitForFunctionAsync("() => document.querySelector('[data-testid=dashboard]')?.getAttribute('aria-busy') === 'false'");

    /// <summary>Học xong bài 1 (chào hỏi) bằng giao diện; trả về số bài tập.</summary>
    private static async Task<int> CompleteFirstLessonAsync(IPage page)
    {
        await LessonRunnerTests.SkipToExercisesAsync(page);
        var total = await page.EvaluateAsync<int>("() => JSON.parse(document.getElementById('lesson-data').textContent).exercises.length");
        for (var n = 1; n <= total; n++)
        {
            await LessonRunnerTests.AnswerCurrentAsync(page);
            await page.GetByTestId("exercise-feedback").WaitForAsync();
            await LessonRunnerTests.NextExerciseAsync(page, n + 1);
        }

        await page.GetByTestId("done-xp").WaitForAsync();
        return total;
    }

    [Fact]
    public async Task Flow1_NewLearner_HomeCta_HangulGroupOne_QuizGivesXpAndStreakDayOne()
    {
        var page = await NewPageAsync(Mobile);
        await page.GotoAsync(server.BaseUrl);

        // Trang chủ → "Bắt đầu miễn phí" → bảng chữ → nhóm đầu tiên (nguyên âm cơ bản)
        await page.GetByTestId("cta-start").ClickAsync();
        await page.Locator("[data-group='nguyen-am']").WaitForAsync();
        await page.GetByTestId("learn-group").First.ClickAsync();
        await page.GetByTestId("hangul-lesson").WaitForAsync();
        new Uri(page.Url).AbsolutePath.ShouldBe("/hangul/nguyen-am");

        // Học từng chữ (10 chữ) → nghe chữ đầu
        await page.Locator("[data-letter-id='v-a'] [data-audio-button] button").First.ClickAsync();
        await page.WaitForFunctionAsync("() => window.__spoken.includes('아')");
        for (var i = 0; i < 9; i++)
        {
            await page.GetByTestId("learn-next").ClickAsync();
        }

        // Quiz 10 câu
        await page.GetByTestId("start-quiz").ClickAsync();
        await page.GetByTestId("quiz").WaitForAsync();
        for (var i = 1; i <= 10; i++)
        {
            await page.WaitForFunctionAsync("n => document.querySelector('[data-testid=quiz-number]')?.textContent === String(n)", i);
            await page.GetByTestId("quiz-choices").Locator("button").First.ClickAsync();
            await page.GetByTestId("quiz-feedback").WaitForAsync();
            await page.GetByTestId("quiz-continue").ClickAsync();
        }

        await page.GetByTestId("quiz-xp").WaitForAsync();
        (await StoredAsync<string>(page, "p.lessons['hangul/nguyen-am'].status")).ShouldBe("completed");
        (await StoredAsync<int>(page, "p.totalXp")).ShouldBeGreaterThanOrEqualTo(5);

        // Dashboard: streak ngày 1, gợi ý bài học đầu tiên
        await page.GetByTestId("bottom-nav").GetByRole(AriaRole.Link, new() { Name = "Tổng quan", Exact = true }).ClickAsync();
        await WaitDashboardLoadedAsync(page);
        (await page.GetByTestId("streak-days").InnerTextAsync()).ShouldBe("1");
        (await page.GetByTestId("continue-cta").GetAttributeAsync("href")).ShouldBe("/learn/beginner/chao-hoi");
    }

    [Fact]
    public async Task Flow2_Lesson_FromCourseMap_CompleteSavesProgress_UnlocksNextLesson()
    {
        var page = await NewPageAsync(Desktop);
        await page.GotoAsync($"{server.BaseUrl}/learn/beginner");

        // Lộ trình → bài 1 (đang mở)
        await page.Locator("[data-lesson='chao-hoi']").ClickAsync();
        await page.GetByTestId("lesson").WaitForAsync();
        var total = await CompleteFirstLessonAsync(page);

        (await page.GetByTestId("done-total").InnerTextAsync()).ShouldBe(total.ToString(System.Globalization.CultureInfo.InvariantCulture));
        (await StoredAsync<string>(page, "p.lessons['beginner/chao-hoi'].status")).ShouldBe("completed");
        (await StoredAsync<int>(page, "Object.keys(p.cards).length")).ShouldBe(14);
        (await StoredAsync<int>(page, "p.totalXp")).ShouldBeGreaterThanOrEqualTo(10);

        // "Bài tiếp theo" mở được; quay lại lộ trình (reload) → bài 1 ✓, bài 2 mở
        (await page.GetByTestId("done-next").GetAttributeAsync("href")).ShouldBe("/learn/beginner/gioi-thieu");
        await page.GetByTestId("done-course").ClickAsync();
        await page.ReloadAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('[data-lesson=chao-hoi]').dataset.state === 'completed'");
        (await page.Locator("[data-lesson='gioi-thieu']").GetAttributeAsync("data-state")).ShouldBe("open");
    }

    [Fact]
    public async Task Flow3_ReviewSession_AfterLessonCardsComeDue_ReviewAll_NothingDueAfter()
    {
        var page = await NewPageAsync(Desktop);
        await page.GotoAsync($"{server.BaseUrl}/learn/beginner/chao-hoi");
        await CompleteFirstLessonAsync(page);

        // "Thời gian trôi": đưa mọi thẻ về đến hạn (thẻ đúng vốn hẹn ngày mai)
        await page.EvaluateAsync("""
            () => { const p = JSON.parse(localStorage.getItem('hanbit.progress.v1'));
              for (const c of Object.values(p.cards)) c.dueAt = new Date(Date.now() - 60_000).toISOString();
              localStorage.setItem('hanbit.progress.v1', JSON.stringify(p)); }
            """);

        // Dashboard → "Ôn tập (14 thẻ)"
        await page.GotoAsync($"{server.BaseUrl}/dashboard");
        await WaitDashboardLoadedAsync(page);
        (await page.GetByTestId("review-cta").InnerTextAsync()).ShouldBe("Ôn tập (14 thẻ)");
        await page.GetByTestId("review-cta").ClickAsync();

        // Ôn hết: Space lật, phím 3 (Được)
        await page.GetByTestId("flashcard").WaitForAsync();
        for (var i = 0; i < 14; i++)
        {
            var id = await page.GetByTestId("flashcard").GetAttributeAsync("data-item-id");
            await page.Keyboard.PressAsync("Space");
            await page.GetByTestId("grades").WaitForAsync();
            await page.Keyboard.PressAsync("3");
            await page.WaitForFunctionAsync(
                "id => document.querySelector('[data-testid=review-done]')?.offsetParent !== null || document.querySelector('[data-testid=flashcard]')?.dataset.itemId !== id", id);
        }

        await page.GetByTestId("review-done").WaitForAsync();
        (await page.GetByTestId("review-count").InnerTextAsync()).ShouldBe("14");
        (await StoredAsync<int>(page, "Object.values(p.cards).filter(c => new Date(c.dueAt) <= new Date()).length")).ShouldBe(0);
        (await StoredAsync<int>(page, "p.activity.reduce((s, a) => s + a.reviews, 0)")).ShouldBe(14);

        // Dashboard: hết thẻ đến hạn
        await page.GotoAsync($"{server.BaseUrl}/dashboard");
        await WaitDashboardLoadedAsync(page);
        (await page.GetByTestId("review-cta").InnerTextAsync()).ShouldBe("Ôn tập");
    }
}
