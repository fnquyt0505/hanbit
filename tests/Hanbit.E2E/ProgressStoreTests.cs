using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>T5.3: progress-store.js — lưu localStorage, reload vẫn giữ tiến độ, dữ liệu hỏng/khác version không làm hỏng trang.</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class ProgressStoreTests(WebServerFixture server)
{
    private static Task<string?> StoredAsync(IPage page, string key = "hanbit.progress.v1") =>
        page.EvaluateAsync<string?>("k => localStorage.getItem(k)", key);

    private static async Task CompleteLessonAsync(IPage page)
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
    }

    [Fact]
    public async Task CompleteLesson_SavesProgress_ReloadKeepsIt_AndUnlocksNextLesson()
    {
        var page = await LessonRunnerTests.OpenLessonAsync(server);
        await CompleteLessonAsync(page);

        (await page.GetByTestId("done-xp").InnerTextAsync()).ShouldMatch(@"^\+\d+ XP$");
        (await page.GetByTestId("done-streak").InnerTextAsync()).ShouldContain("1 ngày");

        var stored = await StoredAsync(page);
        stored.ShouldNotBeNull();
        stored.ShouldContain("\"beginner/chao-hoi\":{\"status\":\"completed\"");
        var cardCount = await page.EvaluateAsync<int>("() => Object.keys(JSON.parse(localStorage.getItem('hanbit.progress.v1')).cards).length");
        cardCount.ShouldBe(14);
        var totalXp = await page.EvaluateAsync<int>("() => JSON.parse(localStorage.getItem('hanbit.progress.v1')).totalXp");
        totalXp.ShouldBeGreaterThanOrEqualTo(10);

        // Reload trang lộ trình: tiến độ vẫn còn → bài 1 hoàn thành, bài 2 mở, bài 3 khóa
        await page.GotoAsync($"{server.BaseUrl}/learn/beginner");
        await page.ReloadAsync();
        await page.WaitForFunctionAsync("() => document.querySelector('[data-lesson=chao-hoi]').dataset.state === 'completed'");
        (await page.Locator("[data-lesson='gioi-thieu']").GetAttributeAsync("data-state")).ShouldBe("open");
        (await page.Locator("[data-lesson='nghe-nghiep']").GetAttributeAsync("data-state")).ShouldBe("locked");
    }

    [Theory]
    [InlineData("{oops", "bị hỏng")]
    [InlineData("{\"schemaVersion\":99,\"lessons\":{}}", "phiên bản khác")]
    public async Task BadStoredData_ShowsNotice_BacksUpRaw_AndPageStillWorks(string raw, string noticeText)
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/hangul"); // cùng origin để ghi localStorage
        await page.EvaluateAsync("v => localStorage.setItem('hanbit.progress.v1', v)", raw);

        await page.GotoAsync($"{server.BaseUrl}/learn/beginner");

        var toast = page.GetByTestId("toast");
        await toast.WaitForAsync();
        (await toast.InnerTextAsync()).ShouldContain(noticeText);
        (await page.Locator("[data-lesson='chao-hoi']").GetAttributeAsync("data-state")).ShouldBe("open");

        var stored = await StoredAsync(page);
        stored.ShouldNotBeNull();
        stored.ShouldContain("\"schemaVersion\":1");
        var backups = await page.EvaluateAsync<string[]>(
            "() => Object.keys(localStorage).filter(k => k.startsWith('hanbit.progress.backup.')).map(k => localStorage.getItem(k))");
        backups.ShouldBe([raw]);
    }

    [Fact]
    public async Task MergeLessonResult_UpsertsCardsReplacesActivityAddsXp()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/learn/beginner");

        var json = await page.EvaluateAsync<string>("""
            async () => {
              const s = await import('/js/progress-store.js');
              const p = { ...s.emptyProgress(new Date('2026-11-24T00:00:00Z')), totalXp: 5,
                cards: { 'kd-1': { itemId: 'kd-1', ease: 2.8 } }, activity: [{ date: '2026-11-23', xp: 5, reviews: 0 }] };
              const next = s.mergeLessonResult(p, {
                lessonKey: 'beginner/chao-hoi', lessonRecord: { status: 'completed', bestScore: 50, completedAt: 'x' },
                newCards: [{ itemId: 'kd-1', ease: 2.5 }, { itemId: 'kd-2', ease: 2.5 }],
                activity: [{ date: '2026-11-23', xp: 5, reviews: 0 }, { date: '2026-11-24', xp: 17, reviews: 0 }], xp: 17 });
              return JSON.stringify({ xp: next.totalXp, cards: Object.keys(next.cards), ease1: next.cards['kd-1'].ease,
                days: next.activity.length, lesson: next.lessons['beginner/chao-hoi'].bestScore, valid: s.isValidProgress(next) });
            }
            """);

        json.ShouldBe("{\"xp\":22,\"cards\":[\"kd-1\",\"kd-2\"],\"ease1\":2.5,\"days\":2,\"lesson\":50,\"valid\":true}");
    }

    [Fact]
    public async Task ThemeChoice_PersistsAcrossReload()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/styleguide");

        await page.GetByTestId("theme-dark").ClickAsync();
        await page.ReloadAsync();

        (await page.Locator("html").GetAttributeAsync("data-theme")).ShouldBe("dark");
        (await StoredAsync(page, "hanbit.settings.v1")).ShouldBe("{\"theme\":\"dark\"}");
    }
}
