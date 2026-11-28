using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>Giai đoạn 10 (T10.1): trang /offline, danh sách vỏ app cho service worker, nút tải khóa học.</summary>
public class OfflineTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task OfflinePage_Renders_NoIndex()
    {
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/offline");

        html.ShouldContain("data-testid=\"offline-page\"");
        html.ShouldContain("Bạn đang offline");
        html.ShouldContain("<meta name=\"robots\" content=\"noindex\" />");
    }

    [Fact]
    public async Task AssetsJson_ListsAppShell_IncludingUnversionedModules()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/offline/assets.json");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Headers.CacheControl!.NoCache.ShouldBeTrue();
        var assets = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("assets")
            .EnumerateArray().Select(a => a.GetString()!).ToList();
        assets.ShouldContain("/js/offline.js");
        assets.ShouldContain("/js/offline-cache.js");
        assets.ShouldContain("/js/api.js");
        assets.ShouldContain("/js/lesson-runner.js");
        assets.ShouldContain("/lib/alpinejs/alpine.min.js");
        assets.ShouldContain("/manifest.webmanifest");
        assets.ShouldNotContain(a => a.Contains("README", StringComparison.OrdinalIgnoreCase));
        assets.ShouldAllBe(a => a.StartsWith('/'));
        foreach (var asset in assets)
        {
            (await client.GetAsync(asset)).StatusCode.ShouldBe(HttpStatusCode.OK, asset);
        }
    }

    [Fact]
    public async Task Layout_LoadsOfflineModule_AndBanner_CoursePageHasDownloadButton()
    {
        using var client = factory.CreateClient();

        var home = await client.GetStringAsync("/");
        home.ShouldContain("/js/offline.js");
        home.ShouldContain("data-testid=\"offline-banner\"");

        var course = await client.GetStringAsync("/learn/beginner");
        course.ShouldContain("data-testid=\"offline-download\"");
        course.ShouldContain("/learn/beginner/chao-hoi");
    }

    [Fact]
    public async Task ServiceWorker_BypassesApiAndAccountRoutes()
    {
        using var client = factory.CreateClient();

        var worker = await client.GetStringAsync("/sw.js");

        worker.ShouldContain(@"/^\/api\//");
        worker.ShouldContain(@"/^\/tai-khoan\//");
        worker.ShouldContain(@"/^\/internal\//");
        worker.ShouldNotContain("export "); // script thường, không phải module
    }
}
