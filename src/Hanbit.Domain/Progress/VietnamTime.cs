namespace Hanbit.Domain.Progress;

/// <summary>Quy đổi thời điểm sang ngày theo giờ Việt Nam (Asia/Ho_Chi_Minh, UTC+7, không có giờ mùa hè).</summary>
public static class VietnamTime
{
    public const string TimeZoneId = "Asia/Ho_Chi_Minh";

    private static readonly TimeZoneInfo Zone = FindZone();

    /// <summary>Ngày (giờ Việt Nam) của <paramref name="instant"/> — VD 2026-11-24T17:30Z → 2026-11-25.</summary>
    public static DateOnly ToDate(DateTimeOffset instant) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    /// <summary>Giờ trong ngày (giờ Việt Nam) của <paramref name="instant"/> — VD 2026-11-28T13:00Z → 20:00.</summary>
    public static TimeOnly ToTime(DateTimeOffset instant) => TimeOnly.FromDateTime(TimeZoneInfo.ConvertTime(instant, Zone).DateTime);

    /// <summary>Thời điểm 00:00 giờ Việt Nam của <paramref name="date"/>.</summary>
    public static DateTimeOffset StartOfDay(DateOnly date) =>
        new(date.ToDateTime(TimeOnly.MinValue), Zone.GetUtcOffset(date.ToDateTime(TimeOnly.MinValue)));

    // Image Docker tối giản có thể thiếu dữ liệu múi giờ → dùng UTC+7 cố định (Việt Nam không đổi giờ từ 1975).
    private static TimeZoneInfo FindZone()
    {
        try
        {
            return TimeZoneInfo.FindSystemTimeZoneById(TimeZoneId);
        }
        catch (Exception ex) when (ex is TimeZoneNotFoundException or InvalidTimeZoneException)
        {
            return TimeZoneInfo.CreateCustomTimeZone(TimeZoneId, TimeSpan.FromHours(7), "Giờ Việt Nam", "Giờ Việt Nam");
        }
    }
}
