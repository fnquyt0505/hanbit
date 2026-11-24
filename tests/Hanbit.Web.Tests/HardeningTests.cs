using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;

using Hanbit.Web.Hosting;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>T7.3: security headers, Cache-Control, rate limiting /api/*, mã hóa HTML.</summary>
public partial class HardeningTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [GeneratedRegex("<script>(.*?)</script>")]
    private static partial Regex InlineScriptRegex();

    [GeneratedRegex("/css/app\\.css\\?v=[^\"]+")]
    private static partial Regex VersionedCssRegex();

    [Fact]
    public async Task HtmlPage_HasSecurityHeaders_AndNoCache()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/");

        var headers = response.Headers;
        headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
        headers.GetValues("X-Frame-Options").ShouldBe(["DENY"]);
        headers.GetValues("Referrer-Policy").ShouldBe(["strict-origin-when-cross-origin"]);
        headers.GetValues("Cross-Origin-Opener-Policy").ShouldBe(["same-origin"]);
        headers.GetValues("Permissions-Policy").Single().ShouldContain("camera=()");
        headers.CacheControl!.NoCache.ShouldBeTrue();

        var csp = headers.GetValues("Content-Security-Policy").Single();
        csp.ShouldContain("default-src 'self'");
        csp.ShouldContain("frame-ancestors 'none'");
        csp.ShouldContain("object-src 'none'");
        csp.ShouldNotContain("script-src 'self' 'unsafe-inline'");
    }

    [Fact]
    public async Task Csp_AllowsTheOnlyInlineScript_ByHash()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        var inlineScripts = InlineScriptRegex().Matches(html).Select(m => m.Groups[1].Value).ToList();
        var script = inlineScripts.ShouldHaveSingleItem();
        var hash = $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(script)))}'";

        hash.ShouldBe(InlineScripts.ThemeInitHash);
        response.Headers.GetValues("Content-Security-Policy").Single().ShouldContain(hash);
    }

    [Fact]
    public async Task VersionedStaticAsset_IsImmutable_UnversionedRevalidates()
    {
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/");
        var versionedCss = VersionedCssRegex().Match(html).Value;
        versionedCss.ShouldNotBeNullOrEmpty();

        using var versioned = await client.GetAsync(versionedCss);
        using var unversioned = await client.GetAsync("/js/speech.js");

        versioned.Headers.CacheControl!.ToString().ShouldBe(HttpHardening.ImmutableCache);
        unversioned.Headers.CacheControl!.NoCache.ShouldBeTrue();
        unversioned.Headers.ETag.ShouldNotBeNull();
        versioned.Headers.GetValues("X-Content-Type-Options").ShouldBe(["nosniff"]);
    }

    [Fact]
    public async Task Api_IsNoStore()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/lessons/beginner/chao-hoi");

        response.Headers.CacheControl!.NoStore.ShouldBeTrue();
        response.Headers.Contains("Content-Security-Policy").ShouldBeFalse();
    }

    [Fact]
    public async Task Api_OverLimit_Returns429ProblemWithRetryAfter_PagesUnaffected()
    {
        using var limited = factory.WithWebHostBuilder(b => b.UseSetting("RateLimit:ApiPermitPerMinute", "3"));
        using var client = limited.CreateClient();

        for (var i = 0; i < 3; i++)
        {
            (await client.GetAsync("/api/hangul/compose?c=ㄱ&v=ㅏ")).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var rejected = await client.GetAsync("/api/hangul/compose?c=ㄱ&v=ㅏ");
        rejected.StatusCode.ShouldBe(HttpStatusCode.TooManyRequests);
        rejected.Headers.RetryAfter.ShouldNotBeNull();
        rejected.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        (await rejected.Content.ReadFromJsonAsync<ProblemDetails>())!.Detail!.ShouldContain("thử lại");

        (await client.GetAsync("/hangul")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Html_DoesNotEncodeVietnameseOrKorean()
    {
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/learn/beginner");

        html.ShouldContain("<title>Tiếng Hàn sơ cấp 1 - Hanbit</title>");
        html.ShouldNotContain("&#x1EBF;");
    }
}
