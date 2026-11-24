using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>T4.4: đi hết một bài học — từ vựng → ngữ pháp → bài tập (chấm qua API) → kết thúc.</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class LessonRunnerTests(WebServerFixture server)
{
    internal const string FakeSpeechScript = """
        (() => {
          window.__spoken = [];
          const synth = {
            getVoices: () => [{ name: 'Fake Korean', lang: 'ko-KR', localService: true }],
            addEventListener() {}, removeEventListener() {}, cancel() {},
            speak(u) { window.__spoken.push(u.text); setTimeout(() => u.onend?.(), 20); },
          };
          Object.defineProperty(window, 'speechSynthesis', { value: synth, configurable: true });
          window.SpeechSynthesisUtterance = class { constructor(text) { this.text = text; } };
        })();
        """;

    internal static async Task<IPage> OpenLessonAsync(WebServerFixture server, string lesson = "chao-hoi", ViewportSize? viewport = null)
    {
        var page = await server.NewPageAsync(viewport: viewport);
        await page.AddInitScriptAsync(FakeSpeechScript);
        await page.GotoAsync($"{server.BaseUrl}/learn/beginner/{lesson}");
        return page;
    }

    /// <summary>Bấm "Tiếp" đúng số thẻ từ vựng rồi số thẻ ngữ pháp (Alpine cập nhật DOM bất đồng bộ → không dựa vào IsVisible).</summary>
    internal static async Task SkipToExercisesAsync(IPage page)
    {
        await page.GetByTestId("vocab-step").WaitForAsync();
        var vocabCount = await page.GetByTestId("vocab-card").CountAsync();
        for (var i = 0; i < vocabCount; i++)
        {
            await page.GetByTestId("vocab-next").ClickAsync();
        }

        var grammarCount = await page.GetByTestId("grammar-card").CountAsync();
        if (grammarCount > 0)
        {
            await page.GetByTestId("grammar-step").WaitForAsync();
            for (var i = 0; i < grammarCount; i++)
            {
                await page.GetByTestId("grammar-next").ClickAsync();
            }
        }

        await page.GetByTestId("exercise-step").WaitForAsync();
    }

    /// <summary>Trả lời bài đang hiện bằng thao tác giao diện (không cần đúng) → trả về loại bài.</summary>
    internal static async Task<string> AnswerCurrentAsync(IPage page)
    {
        var type = await page.GetByTestId("exercise").GetAttributeAsync("data-type") ?? string.Empty;
        switch (type)
        {
            case "multiple_choice" or "listen_choose":
                await page.GetByTestId("choices").Locator("button").First.ClickAsync();
                break;
            case "match_pairs":
                var count = await page.Locator("[data-side=left] button").CountAsync();
                for (var i = 0; i < count; i++)
                {
                    await page.Locator("[data-side=left] button").Nth(i).ClickAsync();
                    await page.Locator("[data-side=right] button").Nth(i).ClickAsync();
                }

                await page.GetByTestId("check").ClickAsync();
                break;
            case "reorder":
                var tokens = await page.Locator("[data-testid=reorder-bank] button").CountAsync();
                for (var i = 0; i < tokens; i++)
                {
                    await page.Locator("[data-testid=reorder-bank] button").Nth(i).ClickAsync();
                }

                await page.GetByTestId("check").ClickAsync();
                break;
            case "type_answer":
                await page.GetByTestId("type-input").FillAsync("답");
                await page.GetByTestId("type-input").PressAsync("Enter");
                break;
        }

        return type;
    }

    /// <summary>Đi tới bài tập có id cho trước (trả lời tạm các bài trước đó).</summary>
    internal static async Task GoToExerciseAsync(IPage page, string exerciseId)
    {
        for (var n = 2; (await page.GetByTestId("exercise").GetAttributeAsync("data-exercise-id")) != exerciseId; n++)
        {
            await AnswerCurrentAsync(page);
            await page.GetByTestId("exercise-feedback").WaitForAsync();
            await NextExerciseAsync(page, n);
        }
    }

    /// <summary>Bấm "Tiếp tục" rồi chờ bài kế tiếp hiện (phản hồi ẩn) hoặc màn kết thúc.</summary>
    internal static async Task NextExerciseAsync(IPage page, int expectedNumber)
    {
        await page.GetByTestId("exercise-next").ClickAsync();
        await page.WaitForFunctionAsync(
            "n => document.querySelector('[data-testid=lesson-done]').offsetParent !== null || document.querySelector('[data-testid=exercise-number]')?.textContent === String(n)",
            expectedNumber);
    }

    [Fact]
    public async Task VocabStep_ShowsOneCard_NextAdvances()
    {
        var page = await OpenLessonAsync(server);
        var first = page.Locator("[data-vocab-id='kd-17298']");
        await first.WaitForAsync();

        await page.GetByTestId("vocab-next").ClickAsync();

        await first.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        (await page.Locator("[data-testid=vocab-card]:visible").CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task FullLesson_AllExerciseTypes_GradedWithFeedback_ThenDone()
    {
        var page = await OpenLessonAsync(server);
        await SkipToExercisesAsync(page);

        var total = await page.EvaluateAsync<int>("() => JSON.parse(document.getElementById('lesson-data').textContent).exercises.length");
        total.ShouldBe(14); // chao-hoi: 7 bài tự soạn + 7 bài sinh

        var types = new HashSet<string>();
        for (var n = 1; n <= total; n++)
        {
            types.Add(await AnswerCurrentAsync(page));
            await page.GetByTestId("exercise-feedback").WaitForAsync();
            await NextExerciseAsync(page, n + 1);
        }

        types.ShouldBe(["multiple_choice", "listen_choose", "match_pairs", "reorder", "type_answer"], ignoreOrder: true);
        await page.GetByTestId("lesson-done").WaitForAsync();
        int.Parse(await page.GetByTestId("done-correct").InnerTextAsync(), System.Globalization.CultureInfo.InvariantCulture)
            .ShouldBeInRange(0, total);
        (await page.GetByTestId("done-total").InnerTextAsync()).ShouldBe("14");
        (await page.GetByTestId("done-score").InnerTextAsync()).ShouldEndWith("%");
        (await page.GetByTestId("done-next").GetAttributeAsync("href")).ShouldBe("/learn/beginner/gioi-thieu");
        (await page.GetByTestId("done-course").GetAttributeAsync("href")).ShouldBe("/learn/beginner");
    }

    [Fact]
    public async Task Summarize_ComputesScoreAndUniqueWrongWords()
    {
        var page = await OpenLessonAsync(server);

        var json = await page.EvaluateAsync<string>("""
            async () => {
              const m = await import('/js/lesson-runner.js');
              const vocab = [{ id: 'kd-1', ko: '가족', vi: 'gia đình' }, { id: 'kd-2', ko: '형', vi: 'anh' }];
              return JSON.stringify(m.summarize([
                { correct: true, vocabId: 'kd-1' },
                { correct: false, vocabId: 'kd-2' },
                { correct: false, vocabId: 'kd-2' },
                { correct: false, vocabId: null },
              ], vocab));
            }
            """);

        json.ShouldBe("{\"total\":4,\"correct\":1,\"score\":25,\"wrongWords\":[{\"id\":\"kd-2\",\"ko\":\"형\",\"vi\":\"anh\"}],\"wrongOther\":1}");
    }

    [Fact]
    public async Task ListenExercise_SpeaksAudioAutomatically()
    {
        var page = await OpenLessonAsync(server);
        await SkipToExercisesAsync(page);

        for (var n = 2; (await page.GetByTestId("exercise").GetAttributeAsync("data-type")) != "listen_choose"; n++)
        {
            await page.GetByTestId("choices").Locator("button").First.ClickAsync();
            await page.GetByTestId("exercise-feedback").WaitForAsync();
            await NextExerciseAsync(page, n);
        }

        await page.WaitForFunctionAsync("() => window.__spoken.length > 0");
    }

    private static async Task ShouldBeCorrectAsync(IPage page)
    {
        var feedback = page.GetByTestId("exercise-feedback");
        await feedback.WaitForAsync();
        (await feedback.InnerTextAsync()).ShouldContain("Chính xác");
    }

    [Fact]
    public async Task MatchPairs_PairCorrectly_UnpairByTapping_IsCorrect()
    {
        var page = await OpenLessonAsync(server);
        await SkipToExercisesAsync(page);
        await GoToExerciseAsync(page, "noi-tu-chao-hoi");

        // Nối nhầm rồi gỡ: chạm lại vế phải đã nối
        await page.Locator("[data-side=left] [data-value='인사']").ClickAsync();
        await page.Locator("[data-side=right] [data-value='sự chúc mừng']").ClickAsync();
        (await page.Locator("[data-side=left] [data-value='인사']").GetAttributeAsync("aria-pressed")).ShouldBe("true");
        await page.Locator("[data-side=right] [data-value='sự chúc mừng']").ClickAsync();
        (await page.Locator("[data-side=left] [data-value='인사']").GetAttributeAsync("aria-pressed")).ShouldBe("false");

        foreach (var (ko, vi) in new[] { ("인사", "sự chào hỏi"), ("축하", "sự chúc mừng"), ("환영", "sự hoan nghênh"), ("감사", "sự cảm tạ") })
        {
            await page.Locator($"[data-side=left] [data-value='{ko}']").ClickAsync();
            await page.Locator($"[data-side=right] [data-value='{vi}']").ClickAsync();
        }

        await page.GetByTestId("check").ClickAsync();
        await ShouldBeCorrectAsync(page);
    }

    [Fact]
    public async Task Reorder_PickInOrder_UnpickWorks_IsCorrect()
    {
        var page = await OpenLessonAsync(server);
        await SkipToExercisesAsync(page);
        await GoToExerciseAsync(page, "sap-xep-gap-lan-dau");

        var bank = page.GetByTestId("reorder-bank");
        await bank.GetByRole(AriaRole.Button, new() { Name = "반갑습니다.", Exact = true }).ClickAsync();
        await page.GetByTestId("reorder-answer").GetByRole(AriaRole.Button, new() { Name = "반갑습니다.", Exact = true }).ClickAsync(); // trả lại
        (await page.GetByTestId("check").IsDisabledAsync()).ShouldBeTrue();

        foreach (var token in new[] { "처음", "뵙겠습니다.", "반갑습니다." })
        {
            await bank.GetByRole(AriaRole.Button, new() { Name = token, Exact = true }).ClickAsync();
        }

        await page.GetByTestId("check").ClickAsync();
        await ShouldBeCorrectAsync(page);
    }

    [Fact]
    public async Task TypeAnswer_WithExtraSpacesAndPunctuation_IsCorrect()
    {
        var page = await OpenLessonAsync(server);
        await SkipToExercisesAsync(page);
        await GoToExerciseAsync(page, "go-bangapseumnida");

        await page.GetByTestId("type-input").FillAsync("  반갑습니다!  ");
        await page.GetByTestId("type-input").PressAsync("Enter");

        await ShouldBeCorrectAsync(page);
    }

    [Fact]
    public async Task Mobile_LessonPage_HasNoHorizontalScroll()
    {
        var page = await OpenLessonAsync(server, viewport: new ViewportSize { Width = 320, Height = 700 });

        var overflow = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");

        overflow.ShouldBeFalse();
    }
}
