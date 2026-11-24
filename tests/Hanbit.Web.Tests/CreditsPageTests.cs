using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>T2.3: trang /nguon-du-lieu — ghi công bắt buộc theo CC BY-SA 2.0 KR.</summary>
public class CreditsPageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private async Task<string> GetCreditsHtmlAsync()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/nguon-du-lieu");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Credits_HasKrdictAttribution_AuthorAndLicense()
    {
        var html = await GetCreditsHtmlAsync();

        html.ShouldContain("data-testid=\"credits-attribution\"");
        html.ShouldContain("한국어기초사전");
        html.ShouldContain("국립국어원");
        html.ShouldContain("CC BY-SA 2.0 KR");
    }

    [Theory]
    [InlineData("https://krdict.korean.go.kr/")]
    [InlineData("https://creativecommons.org/licenses/by-sa/2.0/kr/")]
    [InlineData("https://www.korean.go.kr/")]
    public async Task Credits_LinksToOriginalSourceAndLicense(string url)
    {
        var html = await GetCreditsHtmlAsync();

        html.ShouldContain($"href=\"{url}\"");
    }

    [Fact]
    public async Task Credits_MarksLicenseLinkWithRelLicense()
    {
        var html = await GetCreditsHtmlAsync();

        html.ShouldContain("href=\"https://creativecommons.org/licenses/by-sa/2.0/kr/\" class=\"text-secondary underline underline-offset-2\" rel=\"license noopener\"");
    }

    [Fact]
    public async Task Credits_ExplainsShareAlikeAndOwnContent()
    {
        var html = await GetCreditsHtmlAsync();

        html.ShouldContain("data-testid=\"credits-own\"");
        html.ShouldContain("data-testid=\"credits-third-party\"");
    }

    [Fact]
    public async Task Footer_OnEveryPage_LinksToCredits()
    {
        using var client = factory.CreateClient();

        var home = await client.GetStringAsync("/");

        home.ShouldContain("href=\"/nguon-du-lieu\"");
    }
}
