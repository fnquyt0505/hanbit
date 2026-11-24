using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>T5.5 DoD: xuất → xóa → nhập → khôi phục đúng tiến độ.</summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class ProgressBackupTests(WebServerFixture server)
{
    private const string SeedProgress = """
        {"schemaVersion":1,"createdAt":"2026-11-20T08:00:00.000Z",
         "lessons":{"beginner/chao-hoi":{"status":"completed","bestScore":85,"completedAt":"2026-11-21T08:00:00+00:00"},
                    "hangul/nguyen-am":{"status":"completed","bestScore":90,"completedAt":"2026-11-21T09:00:00+00:00"}},
         "cards":{"kd-17298":{"itemId":"kd-17298","itemType":"vocab","ease":2.5,"intervalDays":1,"repetitions":1,"dueAt":"2026-11-22T17:00:00+00:00","lastReviewedAt":null},
                  "kd-28415":{"itemId":"kd-28415","itemType":"vocab","ease":2.6,"intervalDays":3,"repetitions":2,"dueAt":"2026-11-25T17:00:00+00:00","lastReviewedAt":"2026-11-21T08:00:00+00:00"}},
         "activity":[{"date":"2026-11-21","xp":39,"reviews":0}],
         "totalXp":39}
        """;

    private const string Snapshot =
        "() => { const p = JSON.parse(localStorage.getItem('hanbit.progress.v1')); " +
        "return JSON.stringify({ lessons: Object.keys(p.lessons).sort(), best: p.lessons['beginner/chao-hoi']?.bestScore ?? null, " +
        "cards: Object.keys(p.cards).sort(), ease: p.cards['kd-28415']?.ease ?? null, xp: p.totalXp, days: p.activity.length }); }";

    [Fact]
    public async Task Export_Clear_Import_RestoresProgress()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/hangul");
        await page.EvaluateAsync("v => localStorage.setItem('hanbit.progress.v1', v)", SeedProgress);
        await page.GotoAsync($"{server.BaseUrl}/profile");
        var original = await page.EvaluateAsync<string>(Snapshot);
        await page.WaitForFunctionAsync("() => document.querySelector('[data-testid=profile-xp]')?.textContent === '39'");

        // 1. Xuất
        var download = await page.RunAndWaitForDownloadAsync(() => page.GetByTestId("export").ClickAsync());
        download.SuggestedFilename.ShouldMatch(@"^hanbit-backup-\d{8}\.json$");
        var path = Path.Combine(Path.GetTempPath(), $"hanbit-e2e-{Guid.NewGuid():N}.json");
        await download.SaveAsAsync(path);

        try
        {
            // 2. Xóa
            await page.GetByTestId("clear").ClickAsync();
            await page.GetByTestId("clear-confirm").ClickAsync();
            (await page.EvaluateAsync<int>("() => JSON.parse(localStorage.getItem('hanbit.progress.v1')).totalXp")).ShouldBe(0);
            await page.WaitForFunctionAsync("() => document.querySelector('[data-testid=profile-xp]')?.textContent === '0'");

            // 3. Nhập → xác nhận
            await page.GetByTestId("import-input").SetInputFilesAsync(path);
            var confirm = page.GetByTestId("import-confirm");
            await confirm.WaitForAsync();
            (await confirm.InnerTextAsync()).ShouldContain("2 bài");
            await page.GetByTestId("import-apply").ClickAsync();

            // 4. Khôi phục đúng
            await page.GetByTestId("toast").Filter(new() { HasText = "Đã khôi phục" }).WaitForAsync();
            (await page.EvaluateAsync<string>(Snapshot)).ShouldBe(original);
            await page.WaitForFunctionAsync("() => document.querySelector('[data-testid=profile-xp]')?.textContent === '39'");

            // Lộ trình: bài 1 vẫn hoàn thành sau khôi phục
            await page.GotoAsync($"{server.BaseUrl}/learn/beginner");
            await page.WaitForFunctionAsync("() => document.querySelector('[data-lesson=chao-hoi]').dataset.state === 'completed'");
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportInvalidFile_ShowsError_KeepsCurrentProgress()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/profile");
        var path = Path.Combine(Path.GetTempPath(), $"hanbit-e2e-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, "{\"schemaVersion\": 99}");

        try
        {
            await page.GetByTestId("import-input").SetInputFilesAsync(path);

            var error = page.GetByTestId("import-error");
            await error.WaitForAsync();
            (await error.InnerTextAsync()).ShouldContain("mới hơn");
            (await page.GetByTestId("import-confirm").CountAsync()).ShouldBe(0);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public async Task ImportTamperedFile_ShowsWarningsBeforeApplying()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/profile");
        var path = Path.Combine(Path.GetTempPath(), $"hanbit-e2e-{Guid.NewGuid():N}.json");
        await File.WriteAllTextAsync(path, SeedProgress.Replace("\"ease\":2.6", "\"ease\":42", StringComparison.Ordinal));

        try
        {
            await page.GetByTestId("import-input").SetInputFilesAsync(path);
            await page.GetByTestId("import-warnings").WaitForAsync();
            await page.GetByTestId("import-apply").ClickAsync();

            (await page.EvaluateAsync<double>("() => JSON.parse(localStorage.getItem('hanbit.progress.v1')).cards['kd-28415'].ease")).ShouldBe(5.0);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
