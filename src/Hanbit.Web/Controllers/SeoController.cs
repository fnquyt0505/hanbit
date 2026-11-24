using System.Text;
using System.Xml.Linq;

using Hanbit.Application.Abstractions;
using Hanbit.Web.Hosting;
using Hanbit.Web.Models.Hangul;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hanbit.Web.Controllers;

/// <summary>robots.txt + sitemap.xml (T7.4). Trang cá nhân (dashboard, ôn tập, hồ sơ) không có trong sitemap và gắn noindex.</summary>
public sealed class SeoController(IContentRepository contentRepository, IOptions<SiteOptions> siteOptions) : Controller
{
    private static readonly XNamespace SitemapNs = "http://www.sitemaps.org/schemas/sitemap/0.9";

    [HttpGet("/robots.txt")]
    [ResponseCache(Duration = 3600)]
    public IActionResult Robots()
    {
        var baseUrl = siteOptions.Value.ResolveBaseUrl(Request);
        var body = new StringBuilder()
            .AppendLine("User-agent: *")
            .AppendLine("Allow: /")
            .AppendLine("Disallow: /api/")
            .AppendLine()
            .AppendLine($"Sitemap: {baseUrl}/sitemap.xml")
            .ToString();
        return Content(body, "text/plain", Encoding.UTF8);
    }

    [HttpGet("/sitemap.xml")]
    [ResponseCache(Duration = 3600)]
    public async Task<IActionResult> Sitemap(CancellationToken cancellationToken)
    {
        var baseUrl = siteOptions.Value.ResolveBaseUrl(Request);
        var paths = new List<string> { "/", "/hangul" };
        paths.AddRange(HangulGroupInfo.All.Select(g => $"/hangul/{g.Slug}"));
        foreach (var course in await contentRepository.GetCoursesAsync(cancellationToken))
        {
            paths.Add($"/learn/{course.Slug}");
            paths.AddRange(course.Lessons.OrderBy(l => l.Order).Select(l => $"/learn/{course.Slug}/{l.Slug}"));
        }

        paths.Add("/nguon-du-lieu");

        var document = new XDocument(
            new XDeclaration("1.0", "utf-8", null),
            new XElement(SitemapNs + "urlset", paths.Select(p => new XElement(SitemapNs + "url", new XElement(SitemapNs + "loc", baseUrl + p)))));
        return Content(document.Declaration + Environment.NewLine + document.Root, "application/xml", Encoding.UTF8);
    }
}
