using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>T7.3: CSP không chặn script/Alpine/fetch của site trên các trang chính.</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class CspTests(WebServerFixture server)
{
    [Theory]
    [InlineData("/")]
    [InlineData("/dashboard")]
    [InlineData("/hangul")]
    [InlineData("/hangul/nguyen-am")]
    [InlineData("/learn/beginner")]
    [InlineData("/learn/beginner/chao-hoi")]
    [InlineData("/review")]
    [InlineData("/profile")]
    [InlineData("/styleguide")]
    public async Task Page_HasNoCspViolations_AndAlpineRuns(string path)
    {
        var page = await server.NewPageAsync();
        var violations = new List<string>();
        page.Console += (_, message) =>
        {
            if (message.Text.Contains("Content Security Policy", StringComparison.OrdinalIgnoreCase))
            {
                violations.Add(message.Text);
            }
        };
        await page.AddInitScriptAsync(
            "document.addEventListener('securitypolicyviolation', e => console.error('Content Security Policy violation: ' + e.violatedDirective + ' ' + e.blockedURI));");

        var response = await page.GotoAsync($"{server.BaseUrl}{path}");
        response.ShouldNotBeNull();
        (await response.HeaderValueAsync("content-security-policy")).ShouldNotBeNull();

        // Alpine đã khởi tạo (cần 'unsafe-eval') và store progress đã gọi API (connect-src 'self')
        await page.WaitForFunctionAsync("() => window.Alpine && window.Alpine.store('progress')?.loaded === true");
        violations.ShouldBeEmpty();
    }

    [Fact]
    public async Task ThemeInlineScript_RunsUnderCsp()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/styleguide");
        await page.EvaluateAsync("() => localStorage.setItem('hanbit.settings.v1', JSON.stringify({ theme: 'dark' }))");

        await page.ReloadAsync();

        (await page.Locator("html").GetAttributeAsync("data-theme")).ShouldBe("dark");
    }
}
