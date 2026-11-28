using Hanbit.Domain.Reminders;

namespace Hanbit.Application.Abstractions;

/// <summary>Cài đặt nhắc học + thiết bị nhận thông báo (bảng <c>reminder_settings</c>, <c>push_subscriptions</c>).</summary>
public interface IReminderStore
{
    /// <summary>Cài đặt của tài khoản; chưa có → <see cref="ReminderSettings.Default"/>.</summary>
    Task<ReminderSettings> GetSettingsAsync(string userId, CancellationToken cancellationToken);

    Task SaveSettingsAsync(string userId, bool enabled, TimeOnly time, CancellationToken cancellationToken);

    Task<IReadOnlyList<PushDevice>> GetDevicesAsync(string userId, CancellationToken cancellationToken);

    /// <summary>Thêm/cập nhật thiết bị theo endpoint (endpoint đã thuộc tài khoản khác → chuyển sang tài khoản này).</summary>
    Task SaveDeviceAsync(string userId, PushDevice device, CancellationToken cancellationToken);

    Task RemoveDeviceAsync(string userId, string endpoint, CancellationToken cancellationToken);

    /// <summary>Xóa thiết bị hết hạn (bất kể tài khoản).</summary>
    Task RemoveExpiredDeviceAsync(string endpoint, CancellationToken cancellationToken);

    /// <summary>
    /// Tài khoản có thể cần nhắc lúc này (T9.2): đã bật, giờ nhắc trong [<paramref name="from"/>, <paramref name="to"/>],
    /// hôm nay chưa nhắc, có ít nhất một thiết bị. Quy tắc cuối cùng do <c>ReminderPolicy</c> quyết định.
    /// </summary>
    Task<IReadOnlyList<ReminderCandidate>> GetCandidatesAsync(TimeOnly from, TimeOnly to, DateOnly today, CancellationToken cancellationToken);

    /// <summary>Đánh dấu "đã nhắc hôm nay" nguyên tử; false nếu lần chạy khác đã đánh dấu trước (không gửi đôi).</summary>
    Task<bool> TryClaimAsync(string userId, DateOnly today, CancellationToken cancellationToken);

    /// <summary>Bỏ đánh dấu (không gửi được tới thiết bị nào) → lần chạy sau thử lại.</summary>
    Task ReleaseClaimAsync(string userId, DateOnly today, DateOnly? previous, CancellationToken cancellationToken);
}

/// <summary>Một tài khoản có thể cần nhắc.</summary>
public sealed record ReminderCandidate(string UserId, ReminderSettings Settings);

/// <summary>Gửi Web Push (VAPID). Chỉ đăng ký khi đã cấu hình khóa.</summary>
public interface IPushSender
{
    Task<PushSendStatus> SendAsync(PushDevice device, PushMessage message, CancellationToken cancellationToken);
}
