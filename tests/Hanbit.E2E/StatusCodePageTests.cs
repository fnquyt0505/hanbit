using Shouldly;

namespace Hanbit.E2E;

/// <summary>T2.2a: bấm CTA tới trang chưa có → trang 404 thân thiện, quay về trang chủ được.</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class StatusCodePageTests(WebServerFixture server)
{
    [Fact]
    public async Task OpenMissingPage_Returns404_WithStyledFriendlyPage()
    {
        var page = await server.NewPageAsync();

        var response = await page.GotoAsync($"{server.BaseUrl}/trang-chua-co");

        response.ShouldNotBeNull();
        response.Status.ShouldBe(404);
        (await page.GetByTestId("status-page").IsVisibleAsync()).ShouldBeTrue();
        (await page.TitleAsync()).ShouldContain("Không tìm thấy trang");
        var h1Weight = await page.Locator("h1").EvaluateAsync<string>("el => getComputedStyle(el).fontWeight");
        h1Weight.ShouldBe("700");
    }

    [Fact]
    public async Task MissingPage_BackHomeButton_OpensHome()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/trang-chua-co");

        await page.GetByTestId("status-page").WaitForAsync();
        await page.GetByTestId("status-home").ClickAsync();
        await page.GetByTestId("hero").WaitForAsync();

        new Uri(page.Url).AbsolutePath.ShouldBe("/");
    }
}
