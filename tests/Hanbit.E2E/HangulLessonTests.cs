using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>T3.4: học từng chữ (stepper) → quiz 10 câu chấm qua API → màn kết quả.</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class HangulLessonTests(WebServerFixture server)
{
    private const string FakeSpeechScript = """
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

    private async Task<IPage> OpenGroupAsync(string group)
    {
        var page = await server.NewPageAsync();
        await page.AddInitScriptAsync(FakeSpeechScript);
        await page.GotoAsync($"{server.BaseUrl}/hangul/{group}");
        return page;
    }

    [Fact]
    public async Task Learn_Stepper_ShowsOneLetterAtATime()
    {
        var page = await OpenGroupAsync("nguyen-am");
        var visibleCards = page.Locator("[data-testid=letter-card]:visible");
        ILocator Card(string id) => page.Locator($"[data-testid=letter-card][data-letter-id='{id}']");

        await page.WaitForFunctionAsync("() => document.querySelectorAll('[data-testid=letter-card]:not([style*=\"display: none\"])').length === 1");
        (await visibleCards.CountAsync()).ShouldBe(1);
        (await Card("v-a").IsVisibleAsync()).ShouldBeTrue();

        await page.GetByTestId("learn-next").ClickAsync();
        await Card("v-ya").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        await Card("v-a").WaitForAsync(new() { State = WaitForSelectorState.Hidden });

        await page.GetByTestId("learn-prev").ClickAsync();
        await Card("v-a").WaitForAsync(new() { State = WaitForSelectorState.Visible });
        (await visibleCards.CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task Quiz_AnswerTenQuestions_ShowsFeedbackAndScore()
    {
        var page = await OpenGroupAsync("phu-am-kep");

        for (var i = 0; i < 4; i++)
        {
            await page.GetByTestId("learn-next").ClickAsync();
        }

        await page.GetByTestId("start-quiz").ClickAsync();
        await page.GetByTestId("quiz").WaitForAsync();

        for (var i = 1; i <= 10; i++)
        {
            (await page.GetByTestId("quiz-number").InnerTextAsync()).ShouldBe(i.ToString(System.Globalization.CultureInfo.InvariantCulture));
            await page.GetByTestId("quiz-choices").Locator("button").First.ClickAsync();
            await page.GetByTestId("quiz-feedback").WaitForAsync();

            // Sau khi chấm: luôn có đúng 1 lựa chọn được đánh dấu đúng (kèm icon)
            (await page.Locator("[data-testid=quiz-choices] [data-state=correct]").CountAsync()).ShouldBe(1);
            await page.GetByTestId("quiz-continue").ClickAsync();
        }

        await page.GetByTestId("quiz-done").WaitForAsync();
        var score = int.Parse(await page.GetByTestId("quiz-score").InnerTextAsync(), System.Globalization.CultureInfo.InvariantCulture);
        score.ShouldBeInRange(0, 10);
        (await page.GetByTestId("next-group").IsVisibleAsync()).ShouldBeFalse(); // phụ âm kép là nhóm cuối
    }

    [Fact]
    public async Task Quiz_HtmlAndQuizJson_DoNotContainAnswers()
    {
        var page = await OpenGroupAsync("nguyen-am");
        var quizResponse = page.WaitForResponseAsync(r => r.Url.Contains("/api/hangul/quiz/nguyen-am"));

        await page.GetByRole(AriaRole.Button, new() { Name = "kiểm tra ngay" }).ClickAsync();

        var body = await (await quizResponse).TextAsync();
        body.ShouldNotContain("answer", Case.Insensitive);
        await page.GetByTestId("quiz").WaitForAsync();
    }

    [Fact]
    public async Task Mobile_GroupPage_HasNoHorizontalScroll()
    {
        var page = await server.NewPageAsync(viewport: new ViewportSize { Width = 320, Height = 700 });
        await page.GotoAsync($"{server.BaseUrl}/hangul/phu-am");

        var overflow = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");

        overflow.ShouldBeFalse();
    }
}
