using System.Text.Json;

using Microsoft.Playwright;

using Shouldly;

namespace Hanbit.E2E;

/// <summary>
/// T8.3: progress-sync.js — E2E chạy không có DB nên giả lập trạng thái đã đăng nhập (chèn meta <c>hanbit-user</c> + <c>csrf-token</c>)
/// và giả API <c>/api/progress/sync</c> bằng Playwright route.
/// </summary>
[Collection(WebServerCollection.Name)]
[Trait("Category", "E2E")]
public class ProgressSyncTests(WebServerFixture server)
{
    private const string Lesson = """{"status":"completed","bestScore":90,"completedAt":"2026-11-21T00:00:00.000Z"}""";

    private static string ProgressJson(int xp, params string[] lessons) =>
        $$"""{"schemaVersion":1,"createdAt":"2026-11-20T00:00:00.000Z","lessons":{{{string.Join(',', lessons.Select(l => $"\"{l}\":{Lesson}"))}}},"cards":{},"activity":[],"totalXp":{{xp}}}""";

    private sealed record Captured(JsonElement Body, string? Csrf);

    /// <summary>Mở trang, đặt localStorage, giả API trả <paramref name="serverProgress"/> (null → "chưa có").</summary>
    private async Task<(IPage Page, List<Captured> Requests)> OpenAsync(string? local, string? syncState, string? serverProgress)
    {
        var page = await server.NewPageAsync();
        var requests = new List<Captured>();
        await page.RouteAsync("**/api/progress/sync", async route =>
        {
            var body = JsonDocument.Parse(route.Request.PostData ?? "{}").RootElement.Clone();
            route.Request.Headers.TryGetValue("x-csrf-token", out var csrf);
            lock (requests)
            {
                requests.Add(new Captured(body, csrf));
            }

            await route.FulfillAsync(new RouteFulfillOptions
            {
                ContentType = "application/json",
                Body = $$"""{"progress":{{serverProgress ?? "null"}}}""",
            });
        });

        await page.GotoAsync($"{server.BaseUrl}/hangul");
        await page.EvaluateAsync(
            """
            ([local, sync]) => {
              localStorage.clear();
              if (local) localStorage.setItem('hanbit.progress.v1', local);
              if (sync) localStorage.setItem('hanbit.sync.v1', sync);
            }
            """,
            new[] { local, syncState });
        await page.ReloadAsync();
        return (page, requests);
    }

    /// <summary>Giả "đã đăng nhập" rồi nạp progress-sync.js (module tự đồng bộ khi nạp).</summary>
    private static Task SignInAndLoadSyncAsync(IPage page, string userId = "u1") => page.EvaluateAsync(
        """
        async (userId) => {
          for (const [name, content] of [['hanbit-user', userId], ['csrf-token', 'test-csrf']]) {
            const meta = document.createElement('meta');
            meta.name = name;
            meta.content = content;
            document.head.append(meta);
          }
          await import('/js/progress-sync.js');
        }
        """,
        userId);

    private static Task WaitSyncedAsync(IPage page, int baseXp) => page.WaitForFunctionAsync(
        "xp => JSON.parse(localStorage.getItem('hanbit.sync.v1') ?? '{}').baseXp === xp", baseXp);

    private static Task<int> StoredXpAsync(IPage page) =>
        page.EvaluateAsync<int>("() => JSON.parse(localStorage.getItem('hanbit.progress.v1')).totalXp");

    [Fact]
    public async Task PlanSync_PicksAdoptSyncSwitchAndReplace()
    {
        var page = await server.NewPageAsync();
        await page.GotoAsync($"{server.BaseUrl}/hangul");

        var plans = await page.EvaluateAsync<string[]>(
            """
            async () => {
              const { planSync } = await import('/js/progress-sync.js');
              const { emptyProgress } = await import('/js/progress-store.js');
              const learned = { ...emptyProgress(), totalXp: 30 };
              const show = (p) => `${p.kind}:${p.body.progress ? 'data' : 'null'}:${p.body.baseXp ?? '-'}:${p.body.mode ?? '-'}`;
              return [
                show(planSync(learned, {}, 'u1')),
                show(planSync(emptyProgress(), {}, 'u1')),
                show(planSync(learned, { userId: 'u1', baseXp: 20 }, 'u1')),
                show(planSync(learned, { userId: 'u1', baseXp: 20, replace: true }, 'u1')),
                show(planSync(learned, { userId: 'u2', baseXp: 20 }, 'u1')),
              ];
            }
            """);

        plans.ShouldBe(["adopt:data:0:merge", "adopt:null:-:-", "sync:data:20:merge", "sync:data:20:replace", "switch:null:-:-"]);
    }

    [Fact]
    public async Task FirstSignIn_GuestProgressMergedIntoAccount_LocalReplacedByMerged()
    {
        var (page, requests) = await OpenAsync(
            local: ProgressJson(30, "beginner/chao-hoi"),
            syncState: null,
            serverProgress: ProgressJson(80, "beginner/chao-hoi", "beginner/gioi-thieu"));

        await SignInAndLoadSyncAsync(page);
        await WaitSyncedAsync(page, 80);

        requests.Count.ShouldBe(1);
        requests[0].Csrf.ShouldBe("test-csrf");
        requests[0].Body.GetProperty("baseXp").GetInt32().ShouldBe(0);
        requests[0].Body.GetProperty("mode").GetString().ShouldBe("merge");
        requests[0].Body.GetProperty("progress").GetProperty("totalXp").GetInt32().ShouldBe(30);
        (await StoredXpAsync(page)).ShouldBe(80);
        (await page.EvaluateAsync<bool>("() => 'beginner/gioi-thieu' in JSON.parse(localStorage.getItem('hanbit.progress.v1')).lessons")).ShouldBeTrue();
        (await page.EvaluateAsync<string>("() => JSON.parse(localStorage.getItem('hanbit.sync.v1')).userId")).ShouldBe("u1");
    }

    [Fact]
    public async Task OtherAccountsProgressOnDevice_BackedUp_ReplacedWithThisAccount()
    {
        var (page, requests) = await OpenAsync(
            local: ProgressJson(30, "beginner/chao-hoi"),
            syncState: """{"userId":"someone-else","baseXp":30}""",
            serverProgress: ProgressJson(5));

        await SignInAndLoadSyncAsync(page);
        await WaitSyncedAsync(page, 5);

        requests[0].Body.GetProperty("progress").ValueKind.ShouldBe(JsonValueKind.Null);
        (await StoredXpAsync(page)).ShouldBe(5);
        var backups = await page.EvaluateAsync<string[]>(
            "() => Object.keys(localStorage).filter(k => k.startsWith('hanbit.progress.backup.')).map(k => localStorage.getItem(k))");
        backups.ShouldHaveSingleItem().ShouldContain("\"totalXp\":30");
    }

    [Fact]
    public async Task LocalChange_AfterSync_SendsAgainWithBaseXp()
    {
        var (page, requests) = await OpenAsync(
            local: ProgressJson(40),
            syncState: """{"userId":"u1","baseXp":40}""",
            serverProgress: ProgressJson(40));

        await SignInAndLoadSyncAsync(page);
        await WaitSyncedAsync(page, 40);

        await page.EvaluateAsync(
            """
            async () => {
              const s = await import('/js/progress-store.js');
              s.save({ ...s.load(), totalXp: s.load().totalXp + 12 });
            }
            """);
        // Gửi lại sau debounce (1 s)
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (requests.Count < 2 && DateTime.UtcNow < deadline)
        {
            await page.WaitForTimeoutAsync(100);
        }

        requests.Count.ShouldBe(2);
        requests[1].Body.GetProperty("baseXp").GetInt32().ShouldBe(40);
        requests[1].Body.GetProperty("progress").GetProperty("totalXp").GetInt32().ShouldBe(52);
    }

    [Fact]
    public async Task Logout_SyncsThenClearsDeviceProgress()
    {
        var (page, requests) = await OpenAsync(
            local: ProgressJson(40, "beginner/chao-hoi"),
            syncState: """{"userId":"u1","baseXp":40}""",
            serverProgress: ProgressJson(40, "beginner/chao-hoi"));
        await page.RouteAsync("**/__logout-test", route => route.FulfillAsync(new RouteFulfillOptions { Status = 200, Body = "ok" }));

        await SignInAndLoadSyncAsync(page);
        await WaitSyncedAsync(page, 40);
        await page.EvaluateAsync(
            """
            () => {
              const form = document.createElement('form');
              form.method = 'post';
              form.action = '/__logout-test';
              form.setAttribute('data-logout', '');
              form.innerHTML = '<button type="submit">Đăng xuất</button>';
              document.body.append(form);
            }
            """);

        await page.EvaluateAsync("() => document.querySelector('form[data-logout]').requestSubmit()");
        await page.WaitForURLAsync("**/__logout-test");

        requests.Count.ShouldBe(2); // lần nạp + lần cuối trước khi đăng xuất
        (await page.EvaluateAsync<string?>("() => localStorage.getItem('hanbit.sync.v1')")).ShouldBeNull();
        (await StoredXpAsync(page)).ShouldBe(0);
    }
}
