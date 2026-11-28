using Shouldly;

namespace Hanbit.E2E;

/// <summary>Giai đoạn 9: service worker đăng ký được dưới CSP, hiện thông báo khi nhận push; hàm tiện ích của reminders.js.</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class ReminderTests(WebServerFixture server)
{
    [Fact]
    public async Task ServiceWorker_RegistersAtRootScope_AndActivates()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/hangul");

        var scope = await page.EvaluateAsync<string>(
            """
            async () => {
              const registration = await navigator.serviceWorker.register('/sw.js');
              await navigator.serviceWorker.ready;
              return registration.scope;
            }
            """);

        scope.ShouldBe($"{server.BaseUrl}/");
    }

    [Fact]
    public async Task Base64UrlToBytes_DecodesVapidKey_AndPlatformChecks()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/hangul");

        var result = await page.EvaluateAsync<int[]>(
            """
            async () => {
              const m = await import('/js/reminders.js');
              const key = m.base64UrlToBytes('BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4');
              return [key.length, key[0], key[64], m.pushSupported() ? 1 : 0, m.needsHomeScreen() ? 1 : 0];
            }
            """);

        result.ShouldBe([65, 4, 14, 1, 0]); // Chromium desktop: hỗ trợ push, không cần "Thêm vào Màn hình chính"
    }

    [Fact]
    public async Task Manifest_LinkedAndParsedByBrowser()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/");

        var manifest = await page.EvaluateAsync<string>(
            "async () => (await (await fetch(document.querySelector('link[rel=manifest]').href)).json()).short_name");

        manifest.ShouldBe("Hanbit");
    }

    [Fact]
    public async Task DescribePushError_TranslatesBrowserErrors()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/hangul");

        var messages = await page.EvaluateAsync<string[]>(
            """
            async () => {
              const { describePushError } = await import('/js/reminders.js');
              return [
                describePushError(new DOMException('Registration failed - push service error', 'AbortError')),
                describePushError(new DOMException('Permission denied', 'NotAllowedError')),
                describePushError(new Error('khác')),
              ];
            }
            """);

        messages[0].ShouldContain("Dịch vụ thông báo của trình duyệt từ chối");
        messages[1].ShouldContain("chưa cho phép thông báo");
        messages[2].ShouldContain("thử lại sau");
    }
}
