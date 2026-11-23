using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class StyleguideTests(WebServerFixture server)
{
    private const string LightBg = "rgb(250, 247, 242)"; // --bg #FAF7F2
    private const string DarkBg = "rgb(20, 18, 15)";     // --bg #14120F

    private static Task<string> BodyBackgroundAsync(IPage page) =>
        page.Locator("body").EvaluateAsync<string>("el => getComputedStyle(el).backgroundColor");

    private async Task<IPage> OpenStyleguideAsync(ColorScheme colorScheme)
    {
        var page = await server.NewPageAsync(colorScheme);
        await page.GotoAsync($"{server.BaseUrl}/styleguide");
        return page;
    }

    [Fact]
    public async Task Open_SystemLight_UsesLightTokens()
    {
        var page = await OpenStyleguideAsync(ColorScheme.Light);

        (await BodyBackgroundAsync(page)).ShouldBe(LightBg);
    }

    [Fact]
    public async Task Open_SystemDark_UsesDarkTokens()
    {
        var page = await OpenStyleguideAsync(ColorScheme.Dark);

        (await BodyBackgroundAsync(page)).ShouldBe(DarkBg);
    }

    [Fact]
    public async Task SelectDark_OnLightSystem_SwitchesToDarkTokens()
    {
        var page = await OpenStyleguideAsync(ColorScheme.Light);

        await page.GetByTestId("theme-dark").ClickAsync();

        (await page.Locator("html").GetAttributeAsync("data-theme")).ShouldBe("dark");
        (await BodyBackgroundAsync(page)).ShouldBe(DarkBg);
        (await page.Locator("[data-token='--bg'] [x-text]").InnerTextAsync()).ShouldBe("#14120f");
    }

    [Fact]
    public async Task SelectLight_OnDarkSystem_SwitchesToLightTokens()
    {
        var page = await OpenStyleguideAsync(ColorScheme.Dark);

        await page.GetByTestId("theme-light").ClickAsync();

        (await BodyBackgroundAsync(page)).ShouldBe(LightBg);
    }

    [Theory]
    [InlineData("font-sans-sample", "Be Vietnam Pro")]
    [InlineData("font-ko-sample", "Pretendard")]
    [InlineData("font-hangul-sample", "Noto Serif KR")]
    public async Task Open_FontSamples_UseDesignFonts(string testId, string expectedFamily)
    {
        var page = await OpenStyleguideAsync(ColorScheme.Light);

        var family = await page.GetByTestId(testId).EvaluateAsync<string>("el => getComputedStyle(el).fontFamily");

        family.ShouldStartWith($"\"{expectedFamily}");
    }
}
