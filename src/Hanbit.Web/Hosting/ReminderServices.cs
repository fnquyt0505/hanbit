using Hanbit.Application.Abstractions;
using Hanbit.Application.Reminders;
using Hanbit.Infrastructure.Reminders;

namespace Hanbit.Web.Hosting;

/// <summary>Nhắc học có bật không + khóa công khai VAPID cho trình duyệt (null khi tắt).</summary>
public sealed record ReminderFeature(bool Enabled, string? PublicKey);

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
/// Giai đoạn 9: nhắc học qua Web Push. Chỉ bật khi đã bật tài khoản (DB + Google) VÀ có khóa VAPID
/// (<c>Push:PublicKey</c>/<c>Push:PrivateKey</c>); thiếu → không hiện phần nhắc học, API 404.
/// </summary>
public static class ReminderServices
{
    public static bool AddRemindersIfConfigured(this WebApplicationBuilder builder, bool accountsEnabled)
    {
        var options = builder.Configuration.GetSection(PushOptions.SectionName).Get<PushOptions>() ?? new PushOptions();
        var enabled = accountsEnabled && options.IsConfigured;
        builder.Services.AddSingleton(new ReminderFeature(enabled, enabled ? options.PublicKey : null));
        builder.Services.AddSingleton(builder.Configuration.GetSection(ReminderRunOptions.SectionName).Get<ReminderRunOptions>() ?? new ReminderRunOptions());
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
