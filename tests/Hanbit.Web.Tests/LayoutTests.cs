using System.Net;

using Hanbit.Web.Models.Shared;

using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hanbit.Web.Tests;

public class LayoutTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private async Task<string> GetHtmlAsync(string path)
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task Layout_RendersSidebarBottomNavAndFooter()
    {
        var html = await GetHtmlAsync("/");

        html.ShouldContain("data-testid=\"sidebar\"");
        html.ShouldContain("data-testid=\"bottom-nav\"");
        html.ShouldContain("data-testid=\"footer\"");
        html.ShouldContain("href=\"#noi-dung\"");
        html.ShouldContain("id=\"noi-dung\"");
    }

    [Fact]
    public async Task Layout_LinksEveryNavItem_InSidebarAndBottomNav()
    {
        // Trang không tự chứa link điều hướng → mỗi href xuất hiện đúng 2 lần (sidebar + bottom nav).
        var html = await GetHtmlAsync("/styleguide");

        foreach (var item in MainNavigation.Items)
        {
            // StreakBadge (sidebar + header mobile) cũng là link tới /dashboard → thêm 2 lần.
            var expected = 2 + (item.Href == "/dashboard" ? 2 : 0);
            html.Split($"href=\"{item.Href}\"").Length.ShouldBe(expected + 1, item.Href);
        }
    }

    [Fact]
    public async Task Footer_HasKrdictAttributionAndCreditsLink()
    {
        var html = await GetHtmlAsync("/");

        html.ShouldContain("한국어기초사전");
        html.ShouldContain("국립국어원");
        html.ShouldContain("https://creativecommons.org/licenses/by-sa/2.0/kr/");
        html.ShouldContain("href=\"/nguon-du-lieu\"");
    }

    [Fact]
    public async Task Layout_OnPageOutsideNav_MarksNothingCurrent()
    {
        var html = await GetHtmlAsync("/styleguide");

        html.ShouldNotContain("aria-current=\"page\"");
    }

    [Theory]
    [InlineData("/dashboard", "/dashboard")]
    [InlineData("/learn", "/learn/beginner")]
    [InlineData("/learn/beginner/chao-hoi", "/learn/beginner")]
    [InlineData("/HANGUL/consonants", "/hangul")]
    [InlineData("/review", "/review")]
    public void MainNavigation_ActiveItem_MatchesPathPrefix(string path, string expectedHref)
    {
        MainNavigation.Items.Where(i => i.IsActive(new PathString(path)))
            .ShouldHaveSingleItem().Href.ShouldBe(expectedHref);
    }

    [Theory]
    [InlineData("/")]
    [InlineData("/learning")]
    [InlineData("/styleguide")]
    [InlineData("/nguon-du-lieu")]
    public void MainNavigation_UnrelatedPath_HasNoActiveItem(string path)
    {
        MainNavigation.Items.ShouldNotContain(i => i.IsActive(new PathString(path)));
    }
}
