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
    public async Task ClickToggle_AlpineComponent_ShowsPanel()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync(server.BaseUrl);
        var panel = page.GetByTestId("alpine-panel");
        (await panel.IsVisibleAsync()).ShouldBeFalse();

        await page.GetByTestId("alpine-toggle").ClickAsync();

        await panel.WaitForAsync();
        (await panel.IsVisibleAsync()).ShouldBeTrue();
    }
}
