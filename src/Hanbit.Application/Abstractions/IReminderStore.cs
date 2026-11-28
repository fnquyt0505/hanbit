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
}

/// <summary>Gửi Web Push (VAPID). Chỉ đăng ký khi đã cấu hình khóa.</summary>
public interface IPushSender
{
    Task<PushSendStatus> SendAsync(PushDevice device, PushMessage message, CancellationToken cancellationToken);
}
