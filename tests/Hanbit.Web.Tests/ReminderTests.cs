using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;

using Hanbit.Application.Abstractions;
using Hanbit.Application.Reminders;
using Hanbit.Domain.Progress;
using Hanbit.Domain.Reminders;
using Hanbit.Infrastructure.Persistence;
using Hanbit.Web.Hosting;

using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>Giai đoạn 9: dịch vụ nhắc học — kiểm tra thiết bị (chống SSRF), gửi thử, xóa thiết bị hết hạn.</summary>
public class ReminderServiceTests
{
    private const string P256dh = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string Auth = "BTBZMqHH6r4Tts7J_aSIgg";

    internal sealed class MemoryReminderStore : IReminderStore
    {
        public Dictionary<string, ReminderSettings> Settings { get; } = [];

        public List<(string UserId, PushDevice Device)> Devices { get; } = [];

        public Task<ReminderSettings> GetSettingsAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult(Settings.GetValueOrDefault(userId) ?? ReminderSettings.Default);

        public Task SaveSettingsAsync(string userId, bool enabled, TimeOnly time, CancellationToken cancellationToken)
        {
            Settings[userId] = new ReminderSettings(enabled, time, Settings.GetValueOrDefault(userId)?.LastSentOn);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<PushDevice>> GetDevicesAsync(string userId, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<PushDevice>>([.. Devices.Where(d => d.UserId == userId).Select(d => d.Device)]);

        public Task SaveDeviceAsync(string userId, PushDevice device, CancellationToken cancellationToken)
        {
            Devices.RemoveAll(d => d.Device.Endpoint == device.Endpoint);
            Devices.Add((userId, device));
            return Task.CompletedTask;
        }

        public Task RemoveDeviceAsync(string userId, string endpoint, CancellationToken cancellationToken)
        {
            Devices.RemoveAll(d => d.UserId == userId && d.Device.Endpoint == endpoint);
            return Task.CompletedTask;
        }

        public Task RemoveExpiredDeviceAsync(string endpoint, CancellationToken cancellationToken)
        {
            Devices.RemoveAll(d => d.Device.Endpoint == endpoint);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<ReminderCandidate>> GetCandidatesAsync(TimeOnly from, TimeOnly to, DateOnly today, CancellationToken cancellationToken) =>
            Task.FromResult<IReadOnlyList<ReminderCandidate>>([.. Settings
                .Where(s => s.Value.Enabled && s.Value.Time >= from && s.Value.Time <= to && s.Value.LastSentOn != today
                    && Devices.Any(d => d.UserId == s.Key))
                .Select(s => new ReminderCandidate(s.Key, s.Value))]);

        public Task<bool> TryClaimAsync(string userId, DateOnly today, CancellationToken cancellationToken)
        {
            if (Settings[userId].LastSentOn == today)
            {
                return Task.FromResult(false);
            }

            Settings[userId] = Settings[userId] with { LastSentOn = today };
            return Task.FromResult(true);
        }

        public Task ReleaseClaimAsync(string userId, DateOnly today, DateOnly? previous, CancellationToken cancellationToken)
        {
            if (Settings[userId].LastSentOn == today)
            {
                Settings[userId] = Settings[userId] with { LastSentOn = previous };
            }

            return Task.CompletedTask;
        }
    }

    /// <summary>Dịch vụ push giả: endpoint chứa "gone" → hết hạn, "fail" → lỗi.</summary>
    internal sealed class FakePushSender : IPushSender
    {
        public List<(PushDevice Device, PushMessage Message)> Sent { get; } = [];

        public Task<PushSendStatus> SendAsync(PushDevice device, PushMessage message, CancellationToken cancellationToken)
        {
            if (device.Endpoint.Contains("gone", StringComparison.Ordinal))
            {
                return Task.FromResult(PushSendStatus.Gone);
            }

            if (device.Endpoint.Contains("fail", StringComparison.Ordinal))
            {
                return Task.FromResult(PushSendStatus.Failed);
            }

            Sent.Add((device, message));
            return Task.FromResult(PushSendStatus.Sent);
        }
    }

    private static PushDevice Device(string endpoint) => new(endpoint, P256dh, Auth);

    [Theory]
    [InlineData("https://fcm.googleapis.com/fcm/send/abc", null)]
    [InlineData("https://updates.push.services.mozilla.com/wpush/v2/abc", null)]
    [InlineData("https://db5p.notify.windows.com/w/?token=abc", null)]
    [InlineData("https://web.push.apple.com/QGuQyavXutnMH", null)]
    [InlineData("http://fcm.googleapis.com/fcm/send/abc", "Địa chỉ")] // không https
    [InlineData("https://evil.example/fcm.googleapis.com", "Địa chỉ")]
    [InlineData("https://fcm.googleapis.com.evil.example/x", "Địa chỉ")]
    [InlineData("https://localhost/x", "Địa chỉ")]
    [InlineData("https://fcm.googleapis.com:8443/x", "Địa chỉ")]
    [InlineData("không phải url", "Địa chỉ")]
    public void ValidateDevice_OnlyKnownPushServices(string endpoint, string? expectedError)
    {
        var error = ReminderService.ValidateDevice(Device(endpoint));

        if (expectedError is null)
        {
            error.ShouldBeNull();
        }
        else
        {
            error.ShouldNotBeNull().ShouldContain(expectedError);
        }
    }

    [Theory]
    [InlineData("ngan", Auth)]
    [InlineData(P256dh, "x")]
    [InlineData(P256dh + "<script>", Auth)]
    public void ValidateDevice_BadKeys(string p256dh, string auth)
    {
        ReminderService.ValidateDevice(new PushDevice("https://fcm.googleapis.com/fcm/send/abc", p256dh, auth))
            .ShouldNotBeNull().ShouldContain("Khóa");
    }

    [Fact]
    public async Task UpdateSettings_RejectsInvalidTime_SavesValid()
    {
        var store = new MemoryReminderStore();
        var service = new ReminderService(store, new FakePushSender());

        (await service.UpdateSettingsAsync("u1", true, new TimeOnly(20, 15), default)).ShouldNotBeNull();
        store.Settings.ShouldBeEmpty();
        (await service.UpdateSettingsAsync("u1", true, new TimeOnly(21, 30), default)).ShouldBeNull();
        store.Settings["u1"].ShouldBe(new ReminderSettings(true, new TimeOnly(21, 30), null));
    }

    [Fact]
    public async Task SendTest_SendsToAllDevices_RemovesExpired_CountsFailures()
    {
        var store = new MemoryReminderStore();
        var sender = new FakePushSender();
        var service = new ReminderService(store, sender);
        await service.SubscribeAsync("u1", Device("https://fcm.googleapis.com/fcm/send/phone"), default);
        await service.SubscribeAsync("u1", Device("https://fcm.googleapis.com/fcm/send/gone"), default);
        await service.SubscribeAsync("u1", Device("https://fcm.googleapis.com/fcm/send/fail"), default);
        await service.SubscribeAsync("u2", Device("https://fcm.googleapis.com/fcm/send/other-user"), default);

        var result = await service.SendTestAsync("u1", default);

        result.ShouldBe(new PushTestResult(Sent: 1, Removed: 1, Failed: 1));
        sender.Sent.ShouldHaveSingleItem().Message.ShouldBe(ReminderService.TestMessage);
        store.Devices.Select(d => d.Device.Endpoint).ShouldNotContain("https://fcm.googleapis.com/fcm/send/gone");
        store.Devices.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Subscribe_SameEndpointOtherAccount_MovesDevice()
    {
        var store = new MemoryReminderStore();
        var service = new ReminderService(store, new FakePushSender());
        var device = Device("https://fcm.googleapis.com/fcm/send/shared-browser");

        await service.SubscribeAsync("u1", device, default);
        await service.SubscribeAsync("u2", device, default);

        store.Devices.ShouldHaveSingleItem().UserId.ShouldBe("u2");
    }
}

/// <summary>Giai đoạn 9: tắt (thiếu DB/Google/VAPID) → không có phần nhắc học; manifest + service worker luôn có.</summary>
public class ReminderDisabledTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task NoConfiguration_Api404_ProfileHasNoReminderSection()
    {
        using var client = factory.CreateClient();

        (await client.GetAsync("/api/reminders")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        var html = await client.GetStringAsync("/profile");
        html.ShouldNotContain("data-testid=\"reminders\"");
        html.ShouldNotContain("reminders.js");
    }

    [Fact]
    public void VapidKeysWithoutAccounts_StillDisabled()
    {
        using var configured = factory.WithWebHostBuilder(b => b
            .UseSetting("Push:PublicKey", "public")
            .UseSetting("Push:PrivateKey", "private"));

        configured.Services.GetRequiredService<ReminderFeature>().ShouldBe(new ReminderFeature(false, null));
    }

    [Fact]
    public async Task ManifestAndServiceWorker_ServedFromRoot_LinkedInLayout()
    {
        using var client = factory.CreateClient();

        using var manifest = await client.GetAsync("/manifest.webmanifest");
        manifest.StatusCode.ShouldBe(HttpStatusCode.OK);
        manifest.Content.Headers.ContentType!.MediaType.ShouldBe("application/manifest+json");
        var json = JsonDocument.Parse(await manifest.Content.ReadAsStringAsync()).RootElement;
        json.GetProperty("start_url").GetString().ShouldBe("/dashboard");
        json.GetProperty("display").GetString().ShouldBe("standalone");
        foreach (var icon in json.GetProperty("icons").EnumerateArray())
        {
            (await client.GetAsync(icon.GetProperty("src").GetString())).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var worker = await client.GetAsync("/sw.js");
        worker.StatusCode.ShouldBe(HttpStatusCode.OK);
        worker.Content.Headers.ContentType!.MediaType.ShouldBe("text/javascript");
        (await worker.Content.ReadAsStringAsync()).ShouldContain("addEventListener('push'");

        var html = await client.GetStringAsync("/");
        html.ShouldContain("<link rel=\"manifest\" href=\"/manifest.webmanifest\" />");
        html.ShouldContain("<link rel=\"apple-touch-icon\" href=\"/img/apple-touch-icon.png\" />");
        (await client.GetAsync("/img/apple-touch-icon.png")).StatusCode.ShouldBe(HttpStatusCode.OK);
        (await client.GetAsync("/img/badge-96.png")).StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

/// <summary>Giai đoạn 9: API nhắc học với PostgreSQL + cookie đăng nhập + dịch vụ push giả — CI.</summary>
public sealed partial class ReminderEnabledTests : IAsyncLifetime
{
    private const string PublicKey = "BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4";
    private const string RunToken = "test-run-token-0123456789abcdef0123";
    private static readonly DateTimeOffset At2005Vn = new(2026, 11, 28, 13, 5, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    [GeneratedRegex("<meta name=\"csrf-token\" content=\"([^\"]+)\"")]
    private static partial Regex CsrfMeta();

    private readonly ReminderServiceTests.FakePushSender _sender = new();
    private WebApplicationFactory<Program>? _factory;

    private WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException("Chưa khởi tạo.");

    public async Task InitializeAsync()
    {
        var baseConnection = Environment.GetEnvironmentVariable(DatabaseFactAttribute.EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(baseConnection))
        {
            return;
        }

        var connectionString = new NpgsqlConnectionStringBuilder(baseConnection) { Database = $"hanbit_remind_{Guid.NewGuid():N}" }.ConnectionString;
        await using (var db = new HanbitDbContext(new DbContextOptionsBuilder<HanbitDbContext>().UseNpgsql(connectionString).Options))
        {
            await db.Database.MigrateAsync();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("ConnectionStrings:Hanbit", connectionString)
            .UseSetting("Authentication:Google:ClientId", "test-client.apps.googleusercontent.com")
            .UseSetting("Authentication:Google:ClientSecret", "test-secret")
            .UseSetting("Push:PublicKey", PublicKey)
            .UseSetting("Push:PrivateKey", "yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw")
            .UseSetting("Reminders:RunToken", RunToken)
            .ConfigureTestServices(s =>
            {
                s.AddSingleton<Microsoft.AspNetCore.Hosting.IStartupFilter, TestSignInStartupFilter>();
                s.AddSingleton<IPushSender>(_sender);
                s.AddSingleton<IClock>(new FixedClock(At2005Vn)); // 20:05 giờ VN 28/11
            }));
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

    private async Task<(HttpClient Client, string Csrf)> SignInAsync(string name)
    {
        var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        (await client.PostAsync($"{TestSignInStartupFilter.Path}?name={name}", null)).StatusCode.ShouldBe(HttpStatusCode.NoContent);
        var html = await client.GetStringAsync("/profile");
        html.ShouldContain("data-testid=\"reminders\"");
        html.ShouldContain("reminders.js");
        return (client, CsrfMeta().Match(html).Groups[1].Value);
    }

    private static async Task<HttpResponseMessage> SendAsync(HttpClient client, HttpMethod method, string url, string? csrf, object? body)
    {
        using var request = new HttpRequestMessage(method, url) { Content = body is null ? null : JsonContent.Create(body) };
        if (csrf is not null)
        {
            request.Headers.Add("X-CSRF-TOKEN", csrf);
        }

        return await client.SendAsync(request);
    }

    private static object Subscription(string endpoint) => new
    {
        endpoint,
        keys = new { p256dh = PublicKey, auth = "BTBZMqHH6r4Tts7J_aSIgg" },
    };

    [DatabaseFact]
    public async Task SignedOut_401_AndProfileShowsLoginHint()
    {
        using var client = Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

        (await client.GetAsync("/api/reminders")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
        var html = await client.GetStringAsync("/profile");
        html.ShouldContain("data-testid=\"reminders\"");
        html.ShouldContain("để nhận thông báo nhắc học");
        html.ShouldNotContain("reminders.js");
    }

    [DatabaseFact]
    public async Task Defaults_ThenEnableAtTime_SubscribeDevice_SendTest()
    {
        var (client, csrf) = await SignInAsync("nhac-hoc");
        using (client)
        {
            var defaults = await client.GetFromJsonAsync<JsonElement>("/api/reminders");
            defaults.GetProperty("enabled").GetBoolean().ShouldBeFalse();
            defaults.GetProperty("time").GetString().ShouldBe("20:00");
            defaults.GetProperty("devices").GetInt32().ShouldBe(0);
            defaults.GetProperty("allowedTimes").GetArrayLength().ShouldBe(37);
            defaults.GetProperty("publicKey").GetString().ShouldBe(PublicKey);

            (await SendAsync(client, HttpMethod.Post, "/api/reminders/subscriptions", csrf, Subscription("https://fcm.googleapis.com/fcm/send/phone")))
                .StatusCode.ShouldBe(HttpStatusCode.NoContent);
            (await SendAsync(client, HttpMethod.Put, "/api/reminders", csrf, new { enabled = true, time = "21:30" }))
                .StatusCode.ShouldBe(HttpStatusCode.NoContent);

            var saved = await client.GetFromJsonAsync<JsonElement>("/api/reminders");
            saved.GetProperty("enabled").GetBoolean().ShouldBeTrue();
            saved.GetProperty("time").GetString().ShouldBe("21:30");
            saved.GetProperty("devices").GetInt32().ShouldBe(1);

            using var test = await SendAsync(client, HttpMethod.Post, "/api/reminders/test", csrf, null);
            (await test.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("sent").GetInt32().ShouldBe(1);
            _sender.Sent.ShouldHaveSingleItem().Device.Endpoint.ShouldBe("https://fcm.googleapis.com/fcm/send/phone");

            (await SendAsync(client, HttpMethod.Post, "/api/reminders/subscriptions/remove", csrf, new { endpoint = "https://fcm.googleapis.com/fcm/send/phone" }))
                .StatusCode.ShouldBe(HttpStatusCode.NoContent);
            (await client.GetFromJsonAsync<JsonElement>("/api/reminders")).GetProperty("devices").GetInt32().ShouldBe(0);
        }
    }

    [DatabaseFact]
    public async Task InvalidInput_400_VietnameseMessages_AndCsrfRequired()
    {
        var (client, csrf) = await SignInAsync("sai-du-lieu");
        using (client)
        {
            using var badTime = await SendAsync(client, HttpMethod.Put, "/api/reminders", csrf, new { enabled = true, time = "20:15" });
            badTime.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await badTime.Content.ReadAsStringAsync()).ShouldContain("Giờ nhắc phải từ 05:00 đến 23:00");

            using var badHost = await SendAsync(client, HttpMethod.Post, "/api/reminders/subscriptions", csrf, Subscription("https://evil.example/push"));
            badHost.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
            (await badHost.Content.ReadAsStringAsync()).ShouldContain("Địa chỉ nhận thông báo không hợp lệ");

            (await SendAsync(client, HttpMethod.Put, "/api/reminders", csrf: null, new { enabled = true, time = "20:00" }))
                .StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        }
    }

    [DatabaseFact]
    public async Task DeletingAccount_CascadesSettingsAndDevices()
    {
        var (client, csrf) = await SignInAsync("xoa-tai-khoan");
        using (client)
        {
            await SendAsync(client, HttpMethod.Post, "/api/reminders/subscriptions", csrf, Subscription("https://fcm.googleapis.com/fcm/send/x"));
            await SendAsync(client, HttpMethod.Put, "/api/reminders", csrf, new { enabled = true, time = "20:00" });
        }

        await using var scope = Factory.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<HanbitDbContext>();
        (await db.PushSubscriptions.CountAsync()).ShouldBe(1);
        db.Users.Remove(await db.Users.SingleAsync(u => u.UserName == "xoa-tai-khoan"));
        await db.SaveChangesAsync();

        (await db.PushSubscriptions.CountAsync()).ShouldBe(0);
        (await db.ReminderSettings.CountAsync()).ShouldBe(0);
    }

    [DatabaseFact]
    public async Task InternalRun_TokenRequired_RemindsOnlyLearnerWhoHasNotStudied_Once()
    {
        var (idle, idleCsrf) = await SignInAsync("chua-hoc");
        var (busy, busyCsrf) = await SignInAsync("da-hoc");
        using (idle)
        using (busy)
        {
            foreach (var (client, csrf, name) in new[] { (idle, idleCsrf, "idle"), (busy, busyCsrf, "busy") })
            {
                (await SendAsync(client, HttpMethod.Post, "/api/reminders/subscriptions", csrf, Subscription($"https://fcm.googleapis.com/fcm/send/{name}")))
                    .StatusCode.ShouldBe(HttpStatusCode.NoContent);
                (await SendAsync(client, HttpMethod.Put, "/api/reminders", csrf, new { enabled = true, time = "20:00" }))
                    .StatusCode.ShouldBe(HttpStatusCode.NoContent);
            }

            // "da-hoc" đã học hôm nay (đồng bộ tiến độ lên tài khoản — T8.3)
            var studied = new
            {
                progress = new
                {
                    schemaVersion = 1,
                    createdAt = "2026-11-20T00:00:00Z",
                    lessons = new { },
                    cards = new { },
                    activity = new[] { new { date = "2026-11-28", xp = 12, reviews = 0 } },
                    totalXp = 12,
                },
                baseXp = 0,
            };
            (await SendAsync(busy, HttpMethod.Put, "/api/progress/sync", busyCsrf, studied)).StatusCode.ShouldBe(HttpStatusCode.OK);
        }

        using var cron = Factory.CreateClient();
        (await SendRunAsync(cron, "sai-token")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);

        using var first = await SendRunAsync(cron, RunToken);
        first.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await first.Content.ReadFromJsonAsync<JsonElement>();
        result.GetProperty("candidates").GetInt32().ShouldBe(2);
        result.GetProperty("reminded").GetInt32().ShouldBe(1);
        result.GetProperty("skippedStudied").GetInt32().ShouldBe(1);
        var (device, message) = _sender.Sent.ShouldHaveSingleItem();
        device.Endpoint.ShouldEndWith("/idle");
        message.Tag.ShouldBe("hanbit-reminder");

        // Cron chạy lại trong cùng ngày → không nhắc lần nữa
        using var second = await SendRunAsync(cron, RunToken);
        (await second.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("reminded").GetInt32().ShouldBe(0);
        _sender.Sent.Count.ShouldBe(1);
    }

    private static async Task<HttpResponseMessage> SendRunAsync(HttpClient client, string token)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/reminders/run");
        request.Headers.Add("Authorization", $"Bearer {token}");
        return await client.SendAsync(request);
    }
}

/// <summary>T9.2: một lần chạy gửi nhắc — chọn đúng người, không gửi đôi, giữ lại để thử lần sau khi không gửi được.</summary>
public class ReminderJobTests
{
    private static readonly DateOnly Today = new(2026, 11, 28);
    private static readonly DateTimeOffset At2005Vn = new(2026, 11, 28, 13, 5, 0, TimeSpan.Zero);

    private sealed class FixedClock(DateTimeOffset now) : IClock
    {
        public DateTimeOffset UtcNow => now;
    }

    private sealed class MemoryProgressStore : IProgressStore
    {
        public Dictionary<string, LearnerProgress> Rows { get; } = [];

        public Task<LearnerProgress?> GetAsync(string userId, CancellationToken cancellationToken) => Task.FromResult(Rows.GetValueOrDefault(userId));

        public Task<LearnerProgress> UpdateAsync(string userId, Func<LearnerProgress?, LearnerProgress> update, CancellationToken cancellationToken) =>
            throw new NotSupportedException();
    }

    private static PushDevice Device(string name) => new($"https://fcm.googleapis.com/fcm/send/{name}", "p", "a");

    private static LearnerProgress Progress(IReadOnlyList<DailyActivity> activity, int dueCards = 0) => new(
        1, At2005Vn.AddDays(-10), new Dictionary<string, LessonRecord>(),
        Enumerable.Range(0, dueCards).ToDictionary(i => $"kd-{i}", i => new ReviewCard($"kd-{i}", ItemType.Vocab, 2.5, 1, 1, At2005Vn.AddHours(-1), At2005Vn.AddDays(-1))),
        activity, 100);

    private static (ReminderJob Job, ReminderServiceTests.MemoryReminderStore Store, MemoryProgressStore Progress, ReminderServiceTests.FakePushSender Sender) Create()
    {
        var store = new ReminderServiceTests.MemoryReminderStore();
        var progress = new MemoryProgressStore();
        var sender = new ReminderServiceTests.FakePushSender();
        return (new ReminderJob(store, progress, sender, new FixedClock(At2005Vn)), store, progress, sender);
    }

    [Fact]
    public async Task Run_RemindsOnlyEligible_MessageUsesStreakAndDueCards()
    {
        var (job, store, progress, sender) = Create();
        // u1: 20:00, chưa học hôm nay, chuỗi 2 ngày, 3 thẻ đến hạn → nhắc
        store.Settings["u1"] = new ReminderSettings(true, new TimeOnly(20, 0), null);
        store.Devices.Add(("u1", Device("u1-phone")));
        progress.Rows["u1"] = Progress([new(Today.AddDays(-2), 10, 0), new(Today.AddDays(-1), 10, 0)], dueCards: 3);
        // u2: đã học hôm nay → bỏ qua
        store.Settings["u2"] = new ReminderSettings(true, new TimeOnly(19, 30), null);
        store.Devices.Add(("u2", Device("u2-phone")));
        progress.Rows["u2"] = Progress([new(Today, 15, 2)]);
        // u3: giờ nhắc 21:00 → chưa tới; u4: tắt
        store.Settings["u3"] = new ReminderSettings(true, new TimeOnly(21, 0), null);
        store.Devices.Add(("u3", Device("u3-phone")));
        store.Settings["u4"] = new ReminderSettings(false, new TimeOnly(20, 0), null);
        store.Devices.Add(("u4", Device("u4-phone")));

        var result = await job.RunAsync(default);

        result.ShouldBe(new ReminderRunResult(Candidates: 2, Reminded: 1, SkippedStudied: 1, Sent: 1, Removed: 0, Failed: 0));
        var (device, message) = sender.Sent.ShouldHaveSingleItem();
        device.Endpoint.ShouldEndWith("u1-phone");
        message.ShouldBe(ReminderPolicy.BuildMessage(2, 3));
        store.Settings["u1"].LastSentOn.ShouldBe(Today);
        store.Settings["u2"].LastSentOn.ShouldBeNull();
    }

    [Fact]
    public async Task Run_Twice_SendsOnce()
    {
        var (job, store, _, sender) = Create();
        store.Settings["u1"] = new ReminderSettings(true, new TimeOnly(20, 0), null);
        store.Devices.Add(("u1", Device("phone")));

        await job.RunAsync(default);
        var second = await job.RunAsync(default);

        sender.Sent.Count.ShouldBe(1);
        second.Candidates.ShouldBe(0);
    }

    [Fact]
    public async Task Run_NoProgressOnServer_StillReminds_AllDevicesOfAccount()
    {
        var (job, store, _, sender) = Create();
        store.Settings["u1"] = new ReminderSettings(true, new TimeOnly(20, 0), null);
        store.Devices.Add(("u1", Device("phone")));
        store.Devices.Add(("u1", Device("laptop")));

        (await job.RunAsync(default)).Sent.ShouldBe(2);
        sender.Sent.ShouldAllBe(s => s.Message == ReminderPolicy.BuildMessage(0, 0));
    }

    [Fact]
    public async Task Run_AllDevicesFailOrGone_ReleasesClaim_RemovesGone()
    {
        var (job, store, _, sender) = Create();
        var yesterday = Today.AddDays(-1);
        store.Settings["u1"] = new ReminderSettings(true, new TimeOnly(20, 0), yesterday);
        store.Devices.Add(("u1", Device("gone")));
        store.Devices.Add(("u1", Device("fail")));

        var result = await job.RunAsync(default);

        result.ShouldBe(new ReminderRunResult(1, 0, 0, 0, Removed: 1, Failed: 1));
        sender.Sent.ShouldBeEmpty();
        store.Settings["u1"].LastSentOn.ShouldBe(yesterday); // lần sau thử lại
        store.Devices.ShouldHaveSingleItem().Device.Endpoint.ShouldEndWith("fail");
    }
}

/// <summary>T9.2: xác thực token của endpoint nội bộ.</summary>
public class InternalRemindersAuthTests(WebApplicationFactory<Program> factory) : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Token = "0123456789abcdef0123456789abcdef";

    [Theory]
    [InlineData("Bearer " + Token, true)]
    [InlineData("Bearer  " + Token + " ", true)]
    [InlineData("Bearer " + Token + "x", false)]
    [InlineData("bearer " + Token, false)]
    [InlineData(Token, false)]
    [InlineData("", false)]
    public void IsAuthorized_ExactBearerToken(string header, bool expected)
    {
        Hanbit.Web.Controllers.InternalRemindersController.IsAuthorized(header, Token).ShouldBe(expected);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("ngan-qua", false)]
    [InlineData(Token, true)]
    public void RunToken_RequiresAtLeast32Chars(string? token, bool configured)
    {
        new ReminderRunOptions { RunToken = token }.IsConfigured.ShouldBe(configured);
    }

    [Fact]
    public async Task RemindersDisabled_Run404_EvenWithToken_AndRobotsDisallowsInternal()
    {
        using var configured = factory.WithWebHostBuilder(b => b.UseSetting("Reminders:RunToken", Token));
        using var client = configured.CreateClient();
        using var request = new HttpRequestMessage(HttpMethod.Post, "/internal/reminders/run");
        request.Headers.Add("Authorization", $"Bearer {Token}");

        (await client.SendAsync(request)).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetStringAsync("/robots.txt")).ShouldContain("Disallow: /internal/");
    }
}
