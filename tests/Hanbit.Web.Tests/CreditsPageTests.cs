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

/// <summary>T7.5: tải dữ liệu dẫn xuất (CC BY-SA — chia sẻ cùng giấy phép).</summary>
public class DataDownloadTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("krdict-beginner.json", "application/json", "\"license\": \"CC BY-SA 2.0 KR\"")]
    [InlineData("vocab-reviews.json", "application/json", "\"reviewed\": true")]
    [InlineData("LICENSE-DATA.md", "text/markdown", "CC BY-SA 2.0 KR")]
    public async Task Download_ServesFileAsAttachment(string name, string mediaType, string expectedContent)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/nguon-du-lieu/tai-xuong/{name}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(mediaType);
        response.Content.Headers.ContentDisposition?.DispositionType.ShouldBe("attachment");
        response.Content.Headers.ContentDisposition?.FileNameStar.ShouldBe(name);
        (await response.Content.ReadAsStringAsync()).ShouldContain(expectedContent);
    }

    [Theory]
    [InlineData("/nguon-du-lieu/tai-xuong/hangul.json")]
    [InlineData("/nguon-du-lieu/tai-xuong/..%2F..%2Fappsettings.json")]
    [InlineData("/nguon-du-lieu/tai-xuong/KRDICT-BEGINNER.JSON")]
    public async Task Download_OnlyWhitelistedFiles(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task CreditsPage_LinksEveryDownload()
    {
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/nguon-du-lieu");

        html.ShouldContain("data-testid=\"credits-downloads\"");
        foreach (var file in Hanbit.Web.Controllers.CreditsController.Downloads)
        {
            html.ShouldContain($"href=\"/nguon-du-lieu/tai-xuong/{file.Name}\"");
        }
    }
}
