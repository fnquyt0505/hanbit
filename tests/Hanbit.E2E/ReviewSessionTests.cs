using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>T6.3: phiên ôn tập — lật (Space), đánh giá (phím 1–4 / nút), "Quên" ôn lại trong phiên, lưu localStorage.</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class ReviewSessionTests(WebServerFixture server)
{
    /// <summary>3 thẻ đến hạn (từ có trong bài 1/3): 안녕, 가족, 가수.</summary>
    private const string SeedScript = """
        v => {
          const due = new Date(Date.now() - 3600_000).toISOString();
          const card = (id) => ({ itemId: id, itemType: 'vocab', ease: 2.5, intervalDays: 0, repetitions: 0, dueAt: due, lastReviewedAt: null });
          localStorage.setItem('hanbit.progress.v1', JSON.stringify({
            schemaVersion: 1, createdAt: new Date().toISOString(), lessons: {},
            cards: { 'kd-17298': card('kd-17298'), 'kd-44487': card('kd-44487'), 'kd-62256': card('kd-62256') },
            activity: [], totalXp: 0 }));
        }
        """;

    private async Task<IPage> OpenSeededReviewAsync()
    {
        var page = await server.NewPageAsync();
        await page.AddInitScriptAsync(LessonRunnerTests.FakeSpeechScript);
        await page.GotoAsync($"{server.BaseUrl}/profile");
        await page.EvaluateAsync(SeedScript);
        await page.GotoAsync($"{server.BaseUrl}/review");
        await page.GetByTestId("flashcard").WaitForAsync();
        return page;
    }

    private static Task<string?> CurrentItemAsync(IPage page) => page.GetByTestId("flashcard").GetAttributeAsync("data-item-id");

    [Fact]
    public async Task EmptyProgress_ShowsEmptyState()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/review");

        await page.GetByTestId("review-empty").WaitForAsync();
        (await page.GetByTestId("review").GetAttributeAsync("data-status")).ShouldBe("empty");
    }

    [Fact]
    public async Task Space_FlipsCard_ShowingMeaningAndGradeIntervals()
    {
        var page = await OpenSeededReviewAsync();
        (await page.GetByTestId("flashcard-back").IsVisibleAsync()).ShouldBeFalse();
        (await page.GetByTestId("grades").IsVisibleAsync()).ShouldBeFalse();

        await page.Keyboard.PressAsync("Space");

        await page.GetByTestId("flashcard-back").WaitForAsync();
        (await page.GetByTestId("flashcard-vi").InnerTextAsync()).ShouldNotBeNullOrWhiteSpace();
        (await page.Locator("[data-grade=again]").InnerTextAsync()).ShouldContain("Ngay");
        (await page.Locator("[data-grade=easy]").InnerTextAsync()).ShouldContain("3 ngày");
    }

    [Fact]
    public async Task FullSession_KeysAndAgainRequeue_SavesCardsXpAndActivity()
    {
        var page = await OpenSeededReviewAsync();
        var first = await CurrentItemAsync(page);

        // Thẻ 1: Quên (phím 1) → quay lại cuối phiên
        await page.Keyboard.PressAsync("Space");
        await page.GetByTestId("grades").WaitForAsync();
        await page.Keyboard.PressAsync("1");
        await page.WaitForFunctionAsync("id => document.querySelector('[data-testid=flashcard]')?.dataset.itemId !== id", first);
        (await page.GetByTestId("review-remaining").InnerTextAsync()).ShouldBe("3");

        // Thẻ 2, 3: Được (phím 3); thẻ "Quên" hiện lại cuối cùng → Dễ (nút)
        for (var i = 0; i < 2; i++)
        {
            var id = await CurrentItemAsync(page);
            await page.GetByTestId("flip").ClickAsync();
            await page.Keyboard.PressAsync("3");
            await page.WaitForFunctionAsync("id => document.querySelector('[data-testid=flashcard]')?.dataset.itemId !== id", id);
        }

        (await CurrentItemAsync(page)).ShouldBe(first);
        await page.GetByTestId("flip").ClickAsync();
        await page.Locator("[data-grade=easy]").ClickAsync();

        await page.GetByTestId("review-done").WaitForAsync();
        (await page.GetByTestId("review-count").InnerTextAsync()).ShouldBe("4");
        (await page.GetByTestId("review-xp").InnerTextAsync()).ShouldBe("+4 XP");

        var saved = await page.EvaluateAsync<string>("""
            () => { const p = JSON.parse(localStorage.getItem('hanbit.progress.v1'));
              const due = Object.values(p.cards).filter(c => new Date(c.dueAt) <= new Date()).length;
              return JSON.stringify({ xp: p.totalXp, reviews: p.activity.reduce((s, a) => s + a.reviews, 0), due,
                reps: Object.values(p.cards).map(c => c.repetitions).sort() }); }
            """);
        saved.ShouldBe("{\"xp\":4,\"reviews\":4,\"due\":0,\"reps\":[1,1,1]}");
    }

    [Fact]
    public async Task GradeKeys_IgnoredBeforeFlip()
    {
        var page = await OpenSeededReviewAsync();
        var first = await CurrentItemAsync(page);

        await page.Keyboard.PressAsync("3");

        (await CurrentItemAsync(page)).ShouldBe(first);
        (await page.GetByTestId("review-remaining").InnerTextAsync()).ShouldBe("3");
    }

    [Fact]
    public async Task Mobile_ReviewPage_HasNoHorizontalScroll()
    {
        var page = await server.NewPageAsync(viewport: new ViewportSize { Width = 320, Height = 700 });
        await page.GotoAsync($"{server.BaseUrl}/review");

        var overflow = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");

        overflow.ShouldBeFalse();
    }
}
