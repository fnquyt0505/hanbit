using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hanbit.Web.Tests;

public class HomePageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Get_Root_ReturnsVietnameseHtmlPage()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("<html lang=\"vi\">");
    }

    [Fact]
    public async Task Get_Root_HasHeroThreeBenefitsAndStartCta()
    {
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");

        html.ShouldContain("data-testid=\"hero\"");
        html.Split("<h1").Length.ShouldBe(2);
        var benefits = html[html.IndexOf("data-testid=\"benefits\"", StringComparison.Ordinal)..];
        benefits[..benefits.IndexOf("</ul>", StringComparison.Ordinal)].Split("<li").Length.ShouldBe(4);
        html.ShouldContain("data-testid=\"cta-start\"");
        html.ShouldContain("href=\"/hangul\"");
    }

    [Fact]
    public async Task Get_Root_HasMetaDescription()
    {
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/");

        html.ShouldContain("<meta name=\"description\" content=\"");
    }

    [Fact]
    public async Task Get_UnknownPath_ReturnsNotFound()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/khong-ton-tai");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
