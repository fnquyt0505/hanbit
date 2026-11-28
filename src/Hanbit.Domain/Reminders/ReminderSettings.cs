namespace Hanbit.Domain.Reminders;

/// <summary>
/// Cài đặt nhắc học của một tài khoản (giai đoạn 9). Giờ nhắc theo giờ Việt Nam, bước 30 phút trong
/// [<see cref="EarliestTime"/>, <see cref="LatestTime"/>].
/// </summary>
/// <param name="LastSentOn">Ngày (giờ VN) đã gửi nhắc gần nhất — tối đa 1 lần/ngày.</param>
public sealed record ReminderSettings(bool Enabled, TimeOnly Time, DateOnly? LastSentOn)
{
    public static readonly TimeOnly DefaultTime = new(20, 0);
    public static readonly TimeOnly EarliestTime = new(5, 0);
    public static readonly TimeOnly LatestTime = new(23, 0);
    public const int StepMinutes = 30;

    public static ReminderSettings Default { get; } = new(false, DefaultTime, null);

    /// <summary>Giờ hợp lệ: trong khung cho phép, phút chia hết cho 30, không có giây.</summary>
    public static bool IsValidTime(TimeOnly time) =>
        time >= EarliestTime && time <= LatestTime && time.Minute % StepMinutes == 0 && time.Second == 0 && time.Millisecond == 0;

    /// <summary>Các giờ chọn được (05:00, 05:30, …, 23:00).</summary>
    public static IReadOnlyList<TimeOnly> AllowedTimes { get; } = Enumerable
        .Range(0, ((LatestTime.Hour * 60) - (EarliestTime.Hour * 60)) / StepMinutes + 1)
        .Select(i => EarliestTime.AddMinutes(i * StepMinutes))
        .ToList();
}

/// <summary>Nội dung một thông báo đẩy.</summary>
/// <param name="Url">Đường dẫn trong site mở khi bấm thông báo.</param>
/// <param name="Tag">Thông báo cùng tag thay thế nhau (không chất đống).</param>
public sealed record PushMessage(string Title, string Body, string Url, string Tag);

/// <summary>Một thiết bị đã đăng ký nhận thông báo (PushSubscription của trình duyệt).</summary>
public sealed record PushDevice(string Endpoint, string P256dh, string Auth);

public enum PushSendStatus
{
    Sent,

    /// <summary>Đăng ký hết hạn / bị thu hồi (HTTP 404/410) — xóa đi.</summary>
    Gone,

    Failed,
}
