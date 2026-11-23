using Shouldly;

namespace Hanbit.E2E;

[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class HomePageTests(WebServerFixture server)
{
    [Fact]
    public async Task Open_HomePage_ShowsBrandWithKoreanText()
    {
        var page = await server.Browser.NewPageAsync();

        var response = await page.GotoAsync(server.BaseUrl);

        response.ShouldNotBeNull();
        response.Ok.ShouldBeTrue();
        (await page.TitleAsync()).ShouldContain("Hanbit");
        (await page.Locator("[lang=ko]").First.InnerTextAsync()).ShouldBe("한빛");
    }
}
