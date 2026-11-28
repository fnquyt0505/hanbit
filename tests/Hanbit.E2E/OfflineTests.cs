using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>Giai đoạn 10 (T10.1): xem offline — trang đã mở, trang /offline, tải khóa học, dashboard số liệu lần trước.</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class OfflineTests(WebServerFixture server)
{
    /// <summary>Mở trang (bật service worker), chờ SW cài xong (vỏ app đã lưu) và đang điều khiển trang.</summary>
    private async Task<IPage> OpenControlledAsync(string path)
    {
        var page = await server.NewPageAsync(serviceWorkers: true);
        await page.GotoAsync($"{server.BaseUrl}{path}");
        await page.EvaluateAsync("async () => { await navigator.serviceWorker.ready; }");
        await page.WaitForFunctionAsync("() => navigator.serviceWorker.controller !== null");
        // Mở lại khi SW đã điều khiển → trang HTML được lưu
        await page.ReloadAsync();
        return page;
    }

    [Fact]
    public async Task VisitedPage_ReloadOffline_StillWorks_WithOfflineBanner()
    {
        var page = await OpenControlledAsync("/hangul");
        (await page.GetByTestId("offline-banner").IsVisibleAsync()).ShouldBeFalse();

        await page.Context.SetOfflineAsync(true);
        await page.ReloadAsync();

        (await page.Locator("h1").InnerTextAsync()).ShouldContain("Bảng chữ cái");
        // Alpine (vỏ app lấy từ cache) chạy được → thanh báo offline hiện
        await page.GetByTestId("offline-banner").WaitForAsync();
        (await page.GetByTestId("offline-banner").InnerTextAsync()).ShouldContain("Bạn đang offline");
    }

    [Fact]
    public async Task UnsavedPage_Offline_ShowsOfflinePage_ListingSavedPages()
    {
        var page = await OpenControlledAsync("/hangul");

        await page.Context.SetOfflineAsync(true);
        await page.GotoAsync($"{server.BaseUrl}/credits");

        await page.GetByTestId("offline-page").WaitForAsync();
        await page.GetByTestId("saved-pages").WaitForAsync();
        (await page.GetByTestId("saved-pages").Locator("a[href='/hangul']").CountAsync()).ShouldBe(1);
    }

    [Fact]
    public async Task DownloadCourse_ThenLessonOpensOffline_WithVocabCards()
    {
        var page = await OpenControlledAsync("/learn/beginner");

        await page.GetByTestId("offline-download").ClickAsync();
        await page.GetByTestId("offline-saved").WaitForAsync(new() { Timeout = 30000 });

        await page.Context.SetOfflineAsync(true);
        await page.GotoAsync($"{server.BaseUrl}/learn/beginner/gioi-thieu"); // chưa từng mở — chỉ có nhờ nút tải

        await page.GetByTestId("lesson").WaitForAsync();
        await page.GetByTestId("vocab-card").First.WaitForAsync(); // lesson-runner.js + module import lấy từ cache
        (await page.GetByTestId("offline-banner").IsVisibleAsync()).ShouldBeTrue();

        // Mở lại trang khóa: nút báo đã tải
        await page.GotoAsync($"{server.BaseUrl}/learn/beginner");
        await page.GetByTestId("offline-saved").WaitForAsync();
    }

    [Fact]
    public async Task Dashboard_Offline_ShowsLastSummary_WithStaleNote()
    {
        var page = await OpenControlledAsync("/dashboard");
        await page.WaitForFunctionAsync("() => localStorage.getItem('hanbit.summary.v1') !== null");
        (await page.GetByTestId("dashboard-stale").IsVisibleAsync()).ShouldBeFalse();

        await page.Context.SetOfflineAsync(true);
        await page.ReloadAsync();

        await page.GetByTestId("dashboard-stale").WaitForAsync();
        await page.Locator("[data-skeleton]").First.WaitForAsync(new() { State = WaitForSelectorState.Hidden });
        (await page.GetByTestId("streak-days").InnerTextAsync()).ShouldBe("0");
    }

    [Fact]
    public async Task ApiRequests_NotCached_FailOffline()
    {
        var page = await OpenControlledAsync("/hangul");

        await page.Context.SetOfflineAsync(true);
        var failed = await page.EvaluateAsync<bool>(
            "async () => { try { await fetch('/api/progress/summary', { method: 'POST', headers: { 'Content-Type': 'application/json' }, body: '{}' }); return false; } catch { return true; } }");

        failed.ShouldBeTrue();
    }
}
