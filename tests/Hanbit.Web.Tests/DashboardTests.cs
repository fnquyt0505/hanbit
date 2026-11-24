using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>T2.4: /dashboard render skeleton (tiến độ điền bằng JS) + khóa học từ content/.</summary>
public class DashboardTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private async Task<string> GetDashboardHtmlAsync()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/dashboard");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Dashboard_RendersSkeleton_MarkedBusy()
    {
        var html = await GetDashboardHtmlAsync();

        html.ShouldContain("x-data=\"dashboard\"");
        html.ShouldContain("aria-busy=\"true\"");
        html.Split("data-skeleton").Length.ShouldBe(5);
        html.ShouldContain("/js/dashboard.js");
    }

    [Fact]
    public async Task Dashboard_ListsCoursesFromContent()
    {
        var html = await GetDashboardHtmlAsync();

        html.ShouldContain("data-testid=\"course-list\"");
        html.ShouldContain("href=\"/learn/beginner\"");
        html.ShouldContain("5 bài học");
    }

    [Fact]
    public async Task Dashboard_MarksDashboardNavItemCurrent()
    {
        var html = await GetDashboardHtmlAsync();

        html.Split("href=\"/dashboard\" aria-current=\"page\"").Length.ShouldBe(3);
    }

    [Fact]
    public async Task Dashboard_LoadsModuleBeforeAlpine()
    {
        var html = await GetDashboardHtmlAsync();

        html.IndexOf("/js/dashboard.js", StringComparison.Ordinal)
            .ShouldBeLessThan(html.IndexOf("/lib/alpinejs/alpine.min.js", StringComparison.Ordinal));
    }
}
