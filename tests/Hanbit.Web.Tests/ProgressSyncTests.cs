using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hanbit.Application.Abstractions;
using Hanbit.Application.Progress;
using Hanbit.Domain.Progress;
using Hanbit.Infrastructure.Persistence;
using Hanbit.Web.Controllers.Api;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>T8.3: dịch vụ đồng bộ — gộp / thay / chỉ đọc, dữ liệu xấu bị từ chối (store giả, không cần DB).</summary>
public class ProgressSyncServiceTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 28, 5, 0, 0, TimeSpan.Zero);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private sealed class MemoryStore : IProgressStore
    {
        public Dictionary<string, LearnerProgress> Rows { get; } = [];

        public Task<LearnerProgress?> GetAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(Rows.GetValueOrDefault(userId));

        public Task<LearnerProgress> UpdateAsync(string userId, Func<LearnerProgress?, LearnerProgress> update, CancellationToken cancellationToken)
        {
            var next = update(Rows.GetValueOrDefault(userId));
            Rows[userId] = next;
            return Task.FromResult(next);
        }
    }

    private static readonly JsonSerializerOptions Json = EfProgressStore.JsonOptions;

    private static JsonElement Device(int xp, params string[] completedLessons) => JsonSerializer.SerializeToElement(
        new LearnerProgress(1, Now.AddDays(-1),
            completedLessons.ToDictionary(k => k, _ => new LessonRecord(LessonStatus.Completed, 80, Now.AddHours(-1))),
            new Dictionary<string, ReviewCard>(), [], xp),
        Json);

    private static (ProgressSyncService Service, MemoryStore Store) Create()
    {
        var store = new MemoryStore();
        return (new ProgressSyncService(store, new ProgressImporter(new FixedClock())), store);
    }

    [Fact]
    public async Task Merge_GuestThenSecondDevice_CombinesLessonsAndXp()
    {
        var (service, _) = Create();

        var first = await service.SyncAsync("u1", Device(30, "beginner/a"), 0, ProgressSyncMode.Merge, Json, default);
        var second = await service.SyncAsync("u1", Device(20, "beginner/b"), 0, ProgressSyncMode.Merge, Json, default);

        first.Progress!.TotalXp.ShouldBe(30);
        second.Progress!.TotalXp.ShouldBe(50);
        second.Progress.Lessons.Keys.ShouldBe(["beginner/a", "beginner/b"], ignoreOrder: true);
    }

    [Fact]
    public async Task Replace_OverwritesAccountCopy()
    {
        var (service, store) = Create();
        await service.SyncAsync("u1", Device(30, "beginner/a"), 0, ProgressSyncMode.Merge, Json, default);

        var result = await service.SyncAsync("u1", Device(0), 30, ProgressSyncMode.Replace, Json, default);

        result.Progress!.TotalXp.ShouldBe(0);
        store.Rows["u1"].Lessons.ShouldBeEmpty();
    }

    [Fact]
    public async Task NullProgress_OnlyReads_DoesNotCreateRow()
    {
        var (service, store) = Create();

        (await service.SyncAsync("u1", null, 0, ProgressSyncMode.Merge, Json, default)).Progress.ShouldBeNull();
        store.Rows.ShouldBeEmpty();
    }

    [Fact]
    public async Task InvalidOrNewerSchema_Rejected_NothingStored()
    {
        var (service, store) = Create();
        var newer = JsonDocument.Parse("""{"schemaVersion":99}""").RootElement;

        var result = await service.SyncAsync("u1", newer, 0, ProgressSyncMode.Merge, Json, default);

        result.Progress.ShouldBeNull();
        result.Error.ShouldNotBeNull();
        store.Rows.ShouldBeEmpty();
    }
}

/// <summary>T8.3: tài khoản tắt → endpoint đồng bộ 404, trang không nạp progress-sync.js.</summary>
public class ProgressSyncDisabledTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task AccountsDisabled_SyncIs404_NoSyncScript()
    {
        using var client = factory.CreateClient();

        (await client.GetAsync("/api/progress/sync")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var html = await client.GetStringAsync("/dashboard");
        html.ShouldNotContain("progress-sync.js");
        html.ShouldNotContain("hanbit-user");
    }
}

/// <summary>T8.3: đồng bộ thật với PostgreSQL + cookie đăng nhập (đăng nhập qua endpoint chỉ có trong test) — CI.</summary>
public sealed partial class ProgressSyncEnabledTests : IAsyncLifetime
{
    [GeneratedRegex("<meta name=\"csrf-token\" content=\"([^\"]+)\"")]
    private static partial Regex CsrfMeta();

    private WebApplicationFactory<Program>? _factory;

    private WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException("Chưa khởi tạo.");

    public async Task InitializeAsync()
    {
        var baseConnection = Environment.GetEnvironmentVariable(DatabaseFactAttribute.EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(baseConnection))
        {
            return;
        }

        var connectionString = new NpgsqlConnectionStringBuilder(baseConnection) { Database = $"hanbit_sync_{Guid.NewGuid():N}" }.ConnectionString;
        await using (var db = new HanbitDbContext(new DbContextOptionsBuilder<HanbitDbContext>().UseNpgsql(connectionString).Options))
        {
            await db.Database.MigrateAsync();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("ConnectionStrings:Hanbit", connectionString)
            .UseSetting("Authentication:Google:ClientId", "test-client.apps.googleusercontent.com")
            .UseSetting("Authentication:Google:ClientSecret", "test-secret")
            .ConfigureTestServices(s => s.AddSingleton<IStartupFilter, TestSignInStartupFilter>()));
    }

    public async Task DisposeAsync()
    {
        if (_factory is null)
        {
            return;
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<HanbitDbContext>().Database.EnsureDeletedAsync();
        }

        await _factory.DisposeAsync();
    }

    /// <summary>Client đã đăng nhập + mã CSRF lấy từ meta của trang.</summary>
    private async Task<(HttpClient Client, string Csrf)> SignInAsync(string name)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsync($"{TestSignInStartupFilter.Path}?name={name}", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var html = await client.GetStringAsync("/dashboard");
        html.ShouldContain("progress-sync.js");
        html.ShouldContain("<meta name=\"hanbit-user\"");
        return (client, CsrfMeta().Match(html).Groups[1].Value);
    }

    private static object Device(int xp, params string[] lessons) => new
    {
        schemaVersion = 1,
        createdAt = "2026-11-20T00:00:00Z",
        lessons = lessons.ToDictionary(k => k, _ => new { status = "completed", bestScore = 90, completedAt = "2026-11-21T00:00:00Z" }),
        cards = new Dictionary<string, object>(),
        activity = Array.Empty<object>(),
        totalXp = xp,
    };

    private static async Task<HttpResponseMessage> PutAsync(HttpClient client, string? csrf, object body)
    {
        using var request = new HttpRequestMessage(HttpMethod.Put, "/api/progress/sync") { Content = JsonContent.Create(body) };
        if (csrf is not null)
        {
            request.Headers.Add("X-CSRF-TOKEN", csrf);
        }

        return await client.SendAsync(request);
    }

    [DatabaseFact]
    public async Task NotSignedIn_Returns401()
    {
        using var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.GetAsync("/api/progress/sync")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [DatabaseFact]
    public async Task Put_WithoutCsrfHeader_Returns400()
    {
        var (client, _) = await SignInAsync("csrf-user");
        using (client)
        {
            using var response = await PutAsync(client, csrf: null, new { progress = Device(10), baseXp = 0 });
            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
    }

    [DatabaseFact]
    public async Task TwoDevices_MergeIntoAccount_ThenReplace()
    {
        var (phone, phoneCsrf) = await SignInAsync("hoc-vien");
        var (laptop, laptopCsrf) = await SignInAsync("hoc-vien");
        using (phone)
        using (laptop)
        {
            (await phone.GetFromJsonAsync<JsonElement>("/api/progress/sync")).GetProperty("progress").ValueKind.ShouldBe(JsonValueKind.Null);

            // Điện thoại: dữ liệu khách 30 XP; laptop: dữ liệu khách 20 XP, bài khác
            using var first = await PutAsync(phone, phoneCsrf, new { progress = Device(30, "beginner/chao-hoi"), baseXp = 0, mode = "merge" });
            first.StatusCode.ShouldBe(HttpStatusCode.OK);
            using var second = await PutAsync(laptop, laptopCsrf, new { progress = Device(20, "beginner/gioi-thieu"), baseXp = 0 });
            var merged = (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("progress");
            merged.GetProperty("totalXp").GetInt32().ShouldBe(50);
            merged.GetProperty("lessons").EnumerateObject().Select(p => p.Name).ShouldBe(["beginner/chao-hoi", "beginner/gioi-thieu"], ignoreOrder: true);

            // Điện thoại gửi lại đúng bản cũ (baseXp = 30) → không cộng thêm
            using var resend = await PutAsync(phone, phoneCsrf, new { progress = Device(30, "beginner/chao-hoi"), baseXp = 30 });
            (await resend.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("progress").GetProperty("totalXp").GetInt32().ShouldBe(50);

            // Xóa tiến độ khi đã đăng nhập → thay hẳn
            using var replaced = await PutAsync(phone, phoneCsrf, new { progress = Device(0), baseXp = 50, mode = "replace" });
            var after = await laptop.GetFromJsonAsync<JsonElement>("/api/progress/sync");
            after.GetProperty("progress").GetProperty("totalXp").GetInt32().ShouldBe(0);
            after.GetProperty("progress").GetProperty("lessons").EnumerateObject().ShouldBeEmpty();
        }
    }

    [DatabaseFact]
    public async Task AccountsAreIsolated()
    {
        var (a, aCsrf) = await SignInAsync("nguoi-a");
        var (b, _) = await SignInAsync("nguoi-b");
        using (a)
        using (b)
        {
            (await PutAsync(a, aCsrf, new { progress = Device(42, "beginner/chao-hoi") })).StatusCode.ShouldBe(HttpStatusCode.OK);

            (await b.GetFromJsonAsync<JsonElement>("/api/progress/sync")).GetProperty("progress").ValueKind.ShouldBe(JsonValueKind.Null);
        }
    }

    [DatabaseFact]
    public async Task InvalidProgress_Returns400_WithVietnameseMessage()
    {
        var (client, csrf) = await SignInAsync("bad-data");
        using (client)
        {
            using var response = await PutAsync(client, csrf, new { progress = new { schemaVersion = 99 } });

            response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await response.Content.ReadAsStringAsync()).ShouldContain("phiên bản Hanbit mới hơn");
        }
    }
}
