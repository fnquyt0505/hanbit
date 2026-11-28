using Hanbit.Domain.Progress;

namespace Hanbit.Domain.Reminders;

/// <summary>
/// Khi nào nhắc học (giai đoạn 9 — T9.2, người dùng chốt 2026-11-28): đã bật, đã tới giờ chọn (giờ VN), hôm nay chưa nhắc,
/// hôm nay <b>chưa học</b> (chưa có XP — cùng định nghĩa "ngày có học" của streak). Lịch chạy 15 phút/lần nên trễ tối đa ~15 phút;
/// trễ quá <see cref="LateWindow"/> (lịch bị gián đoạn) thì bỏ, không nhắc lúc khuya.
/// </summary>
public static class ReminderPolicy
{
    public static readonly TimeSpan LateWindow = TimeSpan.FromHours(2);

    public static bool ShouldRemind(ReminderSettings settings, IReadOnlyList<DailyActivity> activity, DateTimeOffset now)
    {
        if (!settings.Enabled)
        {
            return false;
        }

        var today = VietnamTime.ToDate(now);
        if (settings.LastSentOn == today)
        {
            return false;
        }

        var time = VietnamTime.ToTime(now);
        if (time < settings.Time || time - settings.Time >= LateWindow)
        {
            return false;
        }

        return !StreakCalculator.Calculate(activity, today).ActiveToday;
    }

    /// <summary>Nội dung nhắc: nhấn vào chuỗi ngày nếu đang có, kèm số thẻ đến hạn (bấm → trang ôn tập nếu có thẻ).</summary>
    public static PushMessage BuildMessage(int currentStreak, int dueCards)
    {
        var body = dueCards > 0
            ? $"Hôm nay bạn chưa học — {dueCards} thẻ đang chờ ôn. Chỉ cần 5 phút thôi!"
            : "Hôm nay bạn chưa học. Chỉ cần 5 phút thôi!";
        var title = currentStreak > 0 ? $"🔥 Giữ chuỗi {currentStreak} ngày nhé!" : "Đến giờ học tiếng Hàn rồi! 📚";
        return new PushMessage(title, body, dueCards > 0 ? "/review" : "/dashboard", "hanbit-reminder");
    }
}
