using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>T2.2a: 404/lỗi hiển thị trang trong layout, giữ đúng mã HTTP.</summary>
public class StatusCodePageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("/hangul")]
    [InlineData("/khong-ton-tai/abc")]
    public async Task UnknownPage_Returns404_WithFriendlyPageInLayout(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/html");
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("data-testid=\"status-page\"");
        html.ShouldContain("data-status=\"404\"");
        html.ShouldContain("data-testid=\"sidebar\"");
        html.ShouldContain("data-testid=\"footer\"");
        html.ShouldContain("data-testid=\"status-home\"");
    }

    [Fact]
    public async Task ApiPath_404_IsNotRenderedAsHtmlPage()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/khong-ton-tai");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("status-page");
    }

    [Theory]
    [InlineData("/loi/404", HttpStatusCode.NotFound)]
    [InlineData("/loi/500", HttpStatusCode.InternalServerError)]
    [InlineData("/loi/200", HttpStatusCode.NotFound)]
    public async Task DirectAccess_ReturnsMatchingStatusCode(string path, HttpStatusCode expected)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(expected);
    }

    [Fact]
    public async Task ExistingPage_StillReturns200()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldNotContain("status-page");
    }
}
