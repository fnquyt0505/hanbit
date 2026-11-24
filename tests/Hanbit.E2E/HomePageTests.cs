using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class HomePageTests(WebServerFixture server)
{
    [Fact]
    public async Task Open_HomePage_ShowsBrandWithKoreanText()
    {
        var page = await server.NewPageAsync();

        var response = await page.GotoAsync(server.BaseUrl);

        response.ShouldNotBeNull();
        response.Ok.ShouldBeTrue();
        (await page.TitleAsync()).ShouldContain("Hanbit");
        (await page.Locator("[lang=ko]").First.InnerTextAsync()).ShouldBe("한빛");
    }

    [Fact]
    public async Task Open_HomePage_AppliesTailwindClasses()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync(server.BaseUrl);

        var fontWeight = await page.Locator("h1").EvaluateAsync<string>(
            "el => getComputedStyle(el).fontWeight");

        fontWeight.ShouldBe("700");
    }

    [Fact]
    public async Task Mobile_HeroCta_IsVisibleFullWidthAndTallEnough()
    {
        var page = await server.NewPageAsync(viewport: new ViewportSize { Width = 390, Height = 844 });
        await page.GotoAsync(server.BaseUrl);

        var cta = page.GetByTestId("cta-start");
        (await cta.IsVisibleAsync()).ShouldBeTrue();
        (await cta.GetAttributeAsync("href")).ShouldBe("/hangul");

        var box = await cta.BoundingBoxAsync();
        box.ShouldNotBeNull();
        box.Height.ShouldBeGreaterThanOrEqualTo(48);
        box.Width.ShouldBeGreaterThan(390 - 2 * 16 - 1);
    }

    [Theory]
    [InlineData(320)]
    [InlineData(390)]
    [InlineData(1280)]
    public async Task HomePage_HasNoHorizontalScroll(int width)
    {
        var page = await server.NewPageAsync(viewport: new ViewportSize { Width = width, Height = 800 });
        await page.GotoAsync(server.BaseUrl);

        var overflow = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");

        overflow.ShouldBeFalse();
    }
}
