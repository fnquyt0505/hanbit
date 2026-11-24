using System.Net;
using System.Xml.Linq;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>T7.4: meta SEO + Open Graph, noindex trang cá nhân, robots.txt, sitemap.xml, ảnh OG + favicon.</summary>
public class SeoTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private WebApplicationFactory<Program> WithBaseUrl() =>
        factory.WithWebHostBuilder(b => b.UseSetting("Site:BaseUrl", "https://hanbit.example"));

    [Fact]
    public async Task HomePage_HasCanonicalAndOpenGraph_FromConfiguredBaseUrl()
    {
        using var client = WithBaseUrl().CreateClient();

        var html = await client.GetStringAsync("/learn/beginner?utm=x");

        html.ShouldContain("<link rel=\"canonical\" href=\"https://hanbit.example/learn/beginner\" />");
        html.ShouldContain("<meta property=\"og:title\" content=\"Tiếng Hàn sơ cấp 1 - Hanbit\" />");
        html.ShouldContain("<meta property=\"og:url\" content=\"https://hanbit.example/learn/beginner\" />");
        html.ShouldContain("<meta property=\"og:image\" content=\"https://hanbit.example/img/og-image.png\" />");
        html.ShouldContain("<meta name=\"twitter:card\" content=\"summary_large_image\" />");
        html.ShouldContain("<meta property=\"og:locale\" content=\"vi_VN\" />");
        html.ShouldContain("rel=\"icon\"");
        html.ShouldNotContain("noindex");
    }

    [Theory]
    [InlineData("/dashboard")]
    [InlineData("/review")]
    [InlineData("/profile")]
    [InlineData("/styleguide")]
    public async Task PersonalPages_AreNoIndex_WithoutCanonical(string path)
    {
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync(path);

        html.ShouldContain("<meta name=\"robots\" content=\"noindex\" />");
        html.ShouldNotContain("rel=\"canonical\"");
    }

    [Fact]
    public async Task NotFoundPage_IsNoIndex()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/khong-co-trang-nay");

        (await response.Content.ReadAsStringAsync()).ShouldContain("<meta name=\"robots\" content=\"noindex\" />");
    }

    [Fact]
    public async Task Robots_DisallowsApi_PointsToSitemap()
    {
        using var client = WithBaseUrl().CreateClient();

        using var response = await client.GetAsync("/robots.txt");

        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/plain");
        var body = await response.Content.ReadAsStringAsync();
        body.ShouldContain("Disallow: /api/");
        body.ShouldContain("Sitemap: https://hanbit.example/sitemap.xml");
    }

    [Fact]
    public async Task Sitemap_ListsPublicPages_NotPersonalOnes()
    {
        using var client = WithBaseUrl().CreateClient();

        using var response = await client.GetAsync("/sitemap.xml");

        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/xml");
        XNamespace ns = "http://www.sitemaps.org/schemas/sitemap/0.9";
        var locs = XDocument.Parse(await response.Content.ReadAsStringAsync())
            .Descendants(ns + "loc").Select(e => e.Value).ToList();

        locs.ShouldContain("https://hanbit.example/");
        locs.ShouldContain("https://hanbit.example/hangul/phu-am-kep");
        locs.ShouldContain("https://hanbit.example/learn/beginner/thu-trong-tuan");
        locs.ShouldContain("https://hanbit.example/nguon-du-lieu");
        locs.Count.ShouldBe(1 + 1 + 4 + 1 + 5 + 1);
        locs.ShouldAllBe(l => !l.Contains("/dashboard") && !l.Contains("/review") && !l.Contains("/profile"));
    }

    [Theory]
    [InlineData("/img/og-image.png", "image/png")]
    [InlineData("/favicon.svg", "image/svg+xml")]
    public async Task OgImageAndFavicon_AreServed(string path, string mediaType)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe(mediaType);
    }
}
