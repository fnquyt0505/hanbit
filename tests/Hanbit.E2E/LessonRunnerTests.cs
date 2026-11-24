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
    public async Task FullLesson_ChoiceExercises_GradedWithFeedback_ThenDone()
    {
        var page = await OpenLessonAsync(server);
        await SkipToExercisesAsync(page);

        var total = await page.EvaluateAsync<int>(
            "() => JSON.parse(document.getElementById('lesson-data').textContent).exercises.filter(e => ['multiple_choice','listen_choose'].includes(e.type)).length");
        total.ShouldBe(10); // chao-hoi: 3 + 1 bài tự soạn + 6 bài sinh (trắc nghiệm/nghe)

        for (var n = 1; n <= total; n++)
        {
            (await page.GetByTestId("exercise").GetAttributeAsync("data-type")).ShouldBeOneOf("multiple_choice", "listen_choose");
            await page.GetByTestId("choices").Locator("button").First.ClickAsync();
            await page.GetByTestId("exercise-feedback").WaitForAsync();
            (await page.Locator("[data-testid=choices] [data-state=correct]").CountAsync()).ShouldBe(1);
            await NextExerciseAsync(page, n + 1);
        }

        await page.GetByTestId("lesson-done").WaitForAsync();
        int.Parse(await page.GetByTestId("done-correct").InnerTextAsync(), System.Globalization.CultureInfo.InvariantCulture)
            .ShouldBeInRange(0, 10);
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

    [Fact]
    public async Task Mobile_LessonPage_HasNoHorizontalScroll()
    {
        var page = await OpenLessonAsync(server, viewport: new ViewportSize { Width = 320, Height = 700 });

        var overflow = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");

        overflow.ShouldBeFalse();
    }
}
