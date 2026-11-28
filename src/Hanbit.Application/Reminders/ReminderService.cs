using Hanbit.Application.Abstractions;
using Hanbit.Domain.Reminders;

namespace Hanbit.Application.Reminders;

/// <summary>Kết quả gửi thử: số thiết bị nhận được / đã xóa vì hết hạn / lỗi.</summary>
public sealed record PushTestResult(int Sent, int Removed, int Failed);

/// <summary>Bật/tắt, đổi giờ nhắc, đăng ký thiết bị, gửi thử (giai đoạn 9 — T9.1).</summary>
public sealed class ReminderService(IReminderStore store, IPushSender sender)
{
    /// <summary>
    /// Chỉ nhận endpoint của dịch vụ push của trình duyệt — server sẽ POST tới URL này, không cho trỏ tới nơi khác (SSRF).
    /// Chrome/Edge Android: FCM · Firefox: Mozilla · Edge Windows: WNS · Safari/iOS: Apple.
    /// </summary>
    public static readonly IReadOnlyList<string> AllowedPushHosts =
    [
        "fcm.googleapis.com",
        "android.googleapis.com",
        ".push.services.mozilla.com",
        ".notify.windows.com",
        "web.push.apple.com",
        ".push.apple.com",
    ];

    public const int MaxEndpointLength = 2048;

    public static readonly PushMessage TestMessage = new(
        "Hanbit 한빛",
        "Thông báo nhắc học đã bật. Hẹn gặp bạn vào giờ học nhé! 🔥",
        "/dashboard",
        "hanbit-test");

    public Task<ReminderSettings> GetSettingsAsync(string userId, CancellationToken cancellationToken) =>
        store.GetSettingsAsync(userId, cancellationToken);

    public Task<IReadOnlyList<PushDevice>> GetDevicesAsync(string userId, CancellationToken cancellationToken) =>
        store.GetDevicesAsync(userId, cancellationToken);

    /// <returns>Lỗi (tiếng Việt) hoặc null.</returns>
    public async Task<string?> UpdateSettingsAsync(string userId, bool enabled, TimeOnly time, CancellationToken cancellationToken)
    {
        if (!ReminderSettings.IsValidTime(time))
        {
            return $"Giờ nhắc phải từ {ReminderSettings.EarliestTime:HH\\:mm} đến {ReminderSettings.LatestTime:HH\\:mm}, cách nhau 30 phút.";
        }

        await store.SaveSettingsAsync(userId, enabled, time, cancellationToken);
        return null;
    }

    /// <returns>Lỗi (tiếng Việt) hoặc null.</returns>
    public async Task<string?> SubscribeAsync(string userId, PushDevice device, CancellationToken cancellationToken)
    {
        if (ValidateDevice(device) is { } error)
        {
            return error;
        }

        await store.SaveDeviceAsync(userId, device, cancellationToken);
        return null;
    }

    public Task UnsubscribeAsync(string userId, string endpoint, CancellationToken cancellationToken) =>
        store.RemoveDeviceAsync(userId, endpoint, cancellationToken);

    /// <summary>Gửi thử tới mọi thiết bị của tài khoản; thiết bị hết hạn bị xóa.</summary>
    public async Task<PushTestResult> SendTestAsync(string userId, CancellationToken cancellationToken)
    {
        int sent = 0, removed = 0, failed = 0;
        foreach (var device in await store.GetDevicesAsync(userId, cancellationToken))
        {
            switch (await sender.SendAsync(device, TestMessage, cancellationToken))
            {
                case PushSendStatus.Sent:
                    sent++;
                    break;
                case PushSendStatus.Gone:
                    await store.RemoveExpiredDeviceAsync(device.Endpoint, cancellationToken);
                    removed++;
                    break;
                default:
                    failed++;
                    break;
            }
        }

        return new PushTestResult(sent, removed, failed);
    }

    /// <summary>Kiểm tra đăng ký push của trình duyệt (endpoint https thuộc dịch vụ push, khóa base64url hợp lệ).</summary>
    public static string? ValidateDevice(PushDevice device)
    {
        if (string.IsNullOrWhiteSpace(device.Endpoint) || device.Endpoint.Length > MaxEndpointLength
            || !Uri.TryCreate(device.Endpoint, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps || !uri.IsDefaultPort
            || !IsAllowedHost(uri.Host))
        {
            return "Địa chỉ nhận thông báo không hợp lệ.";
        }

        // P-256 public key = 65 byte → 87 ký tự base64url; auth secret = 16 byte → 22 ký tự.
        if (!IsBase64Url(device.P256dh, 80, 100) || !IsBase64Url(device.Auth, 20, 30))
        {
            return "Khóa mã hóa thông báo không hợp lệ.";
        }

        return null;
    }

    private static bool IsAllowedHost(string host) =>
        AllowedPushHosts.Any(allowed => allowed.StartsWith('.')
            ? host.EndsWith(allowed, StringComparison.OrdinalIgnoreCase)
            : host.Equals(allowed, StringComparison.OrdinalIgnoreCase));

    private static bool IsBase64Url(string? value, int minLength, int maxLength) =>
        value is not null && value.Length >= minLength && value.Length <= maxLength
        && value.All(c => char.IsAsciiLetterOrDigit(c) || c is '-' or '_' or '=');
}
