using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>T3.3: bảng ghép âm trên /hangul gọi API và hiển thị âm tiết.</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class HangulComposerTests(WebServerFixture server)
{
    private static ILocator Jamo(IPage page, string part, string jamo) =>
        page.Locator($"[data-part='{part}'] [data-jamo='{jamo}']");

    private static Task WaitForSyllableAsync(IPage page, string syllable) =>
        page.WaitForFunctionAsync(
            "s => document.querySelector('[data-testid=composer-syllable]').textContent === s", syllable);

    [Fact]
    public async Task Default_ShowsGa_RenderedByServer()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/hangul");

        (await page.GetByTestId("composer-syllable").InnerTextAsync()).ShouldBe("가");
        (await page.GetByTestId("composer-romanization").InnerTextAsync()).ShouldBe("ga");
        (await Jamo(page, "c", "ㄱ").GetAttributeAsync("aria-pressed")).ShouldBe("true");
    }

    [Fact]
    public async Task SelectJamo_ComposesHan_ThenRemoveFinal()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/hangul");

        var apiCall = page.WaitForResponseAsync(r => r.Url.Contains("/api/hangul/compose") && r.Url.Contains("f="));
        await Jamo(page, "c", "ㅎ").ClickAsync();
        await Jamo(page, "f", "ㄴ").ClickAsync();
        (await apiCall).Status.ShouldBe(200);

        await WaitForSyllableAsync(page, "한");
        (await page.GetByTestId("composer-romanization").InnerTextAsync()).ShouldBe("han");
        (await Jamo(page, "f", "ㄴ").GetAttributeAsync("aria-pressed")).ShouldBe("true");

        await Jamo(page, "f", "ㄴ").ClickAsync();
        await WaitForSyllableAsync(page, "하");
        (await Jamo(page, "f", "ㄴ").GetAttributeAsync("aria-pressed")).ShouldBe("false");
    }

    [Fact]
    public async Task Mobile_Composer_HasNoHorizontalScroll()
    {
        var page = await server.NewPageAsync(viewport: new ViewportSize { Width = 320, Height = 800 });
        await page.GotoAsync($"{server.BaseUrl}/hangul#ghep-am");

        var overflow = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");

        overflow.ShouldBeFalse();
    }
}
