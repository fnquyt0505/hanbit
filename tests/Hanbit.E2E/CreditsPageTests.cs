using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>T2.3: từ footer mở được trang nguồn dữ liệu, ghi công hiển thị.</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class CreditsPageTests(WebServerFixture server)
{
    [Fact]
    public async Task ClickFooterLink_OpensCreditsWithAttribution()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync(server.BaseUrl);

        await page.GetByRole(AriaRole.Link, new() { Name = "Nguồn dữ liệu & giấy phép" }).ClickAsync();
        await page.GetByTestId("credits").WaitForAsync();

        new Uri(page.Url).AbsolutePath.ShouldBe("/nguon-du-lieu");
        var attribution = await page.GetByTestId("credits-attribution").InnerTextAsync();
        attribution.ShouldContain("한국어기초사전");
        attribution.ShouldContain("국립국어원");
        attribution.ShouldContain("CC BY-SA 2.0 KR");
    }

    [Fact]
    public async Task Mobile_CreditsPage_HasNoHorizontalScroll()
    {
        var page = await server.NewPageAsync(viewport: new ViewportSize { Width = 320, Height = 640 });
        await page.GotoAsync($"{server.BaseUrl}/nguon-du-lieu");

        var overflow = await page.EvaluateAsync<bool>(
            "() => document.documentElement.scrollWidth > document.documentElement.clientWidth");

        overflow.ShouldBeFalse();
    }
}
