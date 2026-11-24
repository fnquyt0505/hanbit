using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>T3.2: bấm ô chữ → đọc tên chữ (phụ âm) hoặc âm tiết với ㅇ câm (nguyên âm).</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class HangulPageTests(WebServerFixture server)
{
    private const string FakeSpeechScript = """
        (() => {
          window.__spoken = [];
          const synth = {
            getVoices: () => [{ name: 'Fake Korean', lang: 'ko-KR', localService: true }],
            addEventListener() {}, removeEventListener() {}, cancel() {},
            speak(u) { window.__spoken.push(u.text); setTimeout(() => u.onend?.(), 50); },
          };
          Object.defineProperty(window, 'speechSynthesis', { value: synth, configurable: true });
          window.SpeechSynthesisUtterance = class { constructor(text) { this.text = text; } };
        })();
        """;

    [Theory]
    [InlineData("c-giyeok", "가")]
    [InlineData("v-a", "아")]
    public async Task ClickTile_SpeaksLetterSound(string letterId, string expected)
    {
        var page = await server.NewPageAsync();
        await page.AddInitScriptAsync(FakeSpeechScript);
        await page.GotoAsync($"{server.BaseUrl}/hangul");

        await page.Locator($"[data-letter-id='{letterId}']").ClickAsync();
        await page.WaitForFunctionAsync("() => window.__spoken.length === 1");

        (await page.EvaluateAsync<string>("() => window.__spoken[0]")).ShouldBe(expected);
    }

    [Fact]
    public async Task ClickStartCta_OnHome_OpensHangulPage()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync(server.BaseUrl);

        await page.GetByTestId("cta-start").ClickAsync();
        await page.Locator("[data-group='nguyen-am']").WaitForAsync();

        new Uri(page.Url).AbsolutePath.ShouldBe("/hangul");
    }

    [Theory]
    [InlineData(320)]
    [InlineData(390)]
    public async Task Mobile_HangulGrid_HasNoHorizontalScroll_AndTouchSizedTiles(int width)
    {
        var page = await server.NewPageAsync(viewport: new ViewportSize { Width = width, Height = 800 });
        await page.GotoAsync($"{server.BaseUrl}/hangul");

        var overflow = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");
        overflow.ShouldBeFalse();

        var box = await page.GetByTestId("hangul-tile").First.BoundingBoxAsync();
        box.ShouldNotBeNull();
        box.Width.ShouldBeGreaterThanOrEqualTo(44);
    }
}
