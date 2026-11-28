using Hanbit.Application.Abstractions;
using Hanbit.Application.Reminders;
using Hanbit.Infrastructure.Reminders;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hanbit.Web.Hosting;

/// <summary>Nhắc học có bật không + khóa công khai VAPID cho trình duyệt (null khi tắt).</summary>
/// <param name="KeyError">Đã đặt khóa VAPID nhưng sai (định dạng / không cùng cặp) → nhắc học tắt, <c>/health</c> Degraded.</param>
public sealed record ReminderFeature(bool Enabled, string? PublicKey, string? KeyError = null);

/// <summary>
/// Token cho <c>POST /internal/reminders/run</c> (T9.2) — Render <c>Reminders__RunToken</c>, GitHub secret <c>REMINDER_RUN_TOKEN</c>.
/// Phải dài ít nhất <see cref="MinTokenLength"/> ký tự (VD <c>openssl rand -hex 32</c>); ngắn hơn coi như chưa đặt.
/// </summary>
public sealed class ReminderRunOptions
{
    public const string SectionName = "Reminders";
    public const int MinTokenLength = 32;

    public string? RunToken { get; set; }

    public bool IsConfigured => RunToken is { Length: >= MinTokenLength };
}

/// <summary>
/// Giai đoạn 9: nhắc học qua Web Push. Chỉ bật khi đã bật tài khoản (DB + Google) VÀ có cặp khóa VAPID hợp lệ
/// (<c>Push:PublicKey</c>/<c>Push:PrivateKey</c>); thiếu → không hiện phần nhắc học, API 404.
/// </summary>
public static class ReminderServices
{
    public static bool AddRemindersIfConfigured(this WebApplicationBuilder builder, bool accountsEnabled)
    {
        var options = builder.Configuration.GetSection(PushOptions.SectionName).Get<PushOptions>() ?? new PushOptions();
        options.PublicKey = WebPushCrypto.NormalizeKey(options.PublicKey);
        options.PrivateKey = WebPushCrypto.NormalizeKey(options.PrivateKey);
        var keyError = options.IsConfigured ? WebPushCrypto.ValidateVapidKeys(options.PublicKey!, options.PrivateKey!) : null;
        var enabled = accountsEnabled && options.IsConfigured && keyError is null;

        builder.Services.AddSingleton(new ReminderFeature(enabled, enabled ? options.PublicKey : null, keyError));
        builder.Services.AddSingleton(builder.Configuration.GetSection(ReminderRunOptions.SectionName).Get<ReminderRunOptions>() ?? new ReminderRunOptions());
        builder.Services.AddHealthChecks().AddCheck<PushKeysHealthCheck>("push", failureStatus: HealthStatus.Degraded);
        if (!enabled)
        {
            return false;
        }

        builder.Services.AddSingleton(options);
        builder.Services.AddHttpClient<IPushSender, WebPushSender>(client => client.Timeout = TimeSpan.FromSeconds(15));
        builder.Services.AddScoped<ReminderService>();
        builder.Services.AddScoped<ReminderJob>();
        return true;
    }
}

/// <summary>Khóa VAPID đặt sai → Degraded (không lộ giá trị khóa); chưa đặt → Healthy (nhắc học là tùy chọn).</summary>
public sealed class PushKeysHealthCheck(ReminderFeature feature, ILogger<PushKeysHealthCheck> logger) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        if (feature.KeyError is null)
        {
            return Task.FromResult(HealthCheckResult.Healthy());
        }

        logger.LogError("Khóa VAPID sai: {Error}", feature.KeyError);
        return Task.FromResult(new HealthCheckResult(context.Registration.FailureStatus, feature.KeyError));
    }
}
