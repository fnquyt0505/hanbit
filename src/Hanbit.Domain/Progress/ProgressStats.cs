namespace Hanbit.Domain.Progress;

/// <summary>Trạng thái ghi nhớ của một từ.</summary>
public enum WordStatus
{
    /// <summary>Khoảng ôn &lt; <see cref="ProgressStatsCalculator.MasteredIntervalDays"/> ngày.</summary>
    Learning,

    /// <summary>Đã thuộc: khoảng ôn ≥ <see cref="ProgressStatsCalculator.MasteredIntervalDays"/> ngày.</summary>
    Mastered,
}

/// <summary>Một ô trên lịch streak.</summary>
/// <param name="Level">0 (không học) … 4 (học nhiều) theo XP trong ngày.</param>
public sealed record CalendarDay(DateOnly Date, int Xp, int Reviews, int Level);

/// <summary>Thống kê trang hồ sơ.</summary>
/// <param name="Calendar">Các ngày từ thứ Hai của tuần đầu đến hôm nay (đủ <see cref="ProgressStatsCalculator.CalendarWeeks"/> tuần).</param>
public sealed record ProgressStats(
    int MasteredWords,
    int LearningWords,
    int TotalReviews,
    int ActiveDays,
    Streak Streak,
    IReadOnlyList<CalendarDay> Calendar);

/// <summary>Tính thống kê hồ sơ (logic thuần).</summary>
public static class ProgressStatsCalculator
{
    /// <summary>Ngưỡng "đã thuộc" — khoảng ôn ≥ 21 ngày (giống thẻ "mature" của Anki).</summary>
    public const int MasteredIntervalDays = 21;

    /// <summary>Số tuần hiển thị trên lịch streak (≈ 4 tháng).</summary>
    public const int CalendarWeeks = 17;

    public static WordStatus StatusOf(ReviewCard card) =>
        card.IntervalDays >= MasteredIntervalDays ? WordStatus.Mastered : WordStatus.Learning;

    /// <summary>Mức màu theo XP trong ngày: 0 · 1–10 · 11–25 · 26–50 · &gt; 50.</summary>
    public static int LevelOf(int xp) => xp switch
    {
        <= 0 => 0,
        <= 10 => 1,
        <= 25 => 2,
        <= 50 => 3,
        _ => 4,
    };

    public static ProgressStats Calculate(IEnumerable<ReviewCard> cards, IReadOnlyList<DailyActivity> activity, DateOnly today)
    {
        var vocab = cards.Where(c => c.ItemType == ItemType.Vocab).DistinctBy(c => c.ItemId, StringComparer.Ordinal).ToList();
        var byDate = activity.GroupBy(a => a.Date).ToDictionary(g => g.Key, g => (Xp: g.Sum(a => a.Xp), Reviews: g.Sum(a => a.Reviews)));

        // Tuần bắt đầu thứ Hai; tuần cuối chứa hôm nay
        var daysSinceMonday = ((int)today.DayOfWeek + 6) % 7;
        var start = today.AddDays(-daysSinceMonday - (7 * (CalendarWeeks - 1)));
        var calendar = new List<CalendarDay>();
        for (var day = start; day <= today; day = day.AddDays(1))
        {
            var (xp, reviews) = byDate.GetValueOrDefault(day);
            calendar.Add(new CalendarDay(day, xp, reviews, LevelOf(xp)));
        }

        return new ProgressStats(
            vocab.Count(c => StatusOf(c) == WordStatus.Mastered),
            vocab.Count(c => StatusOf(c) == WordStatus.Learning),
            activity.Sum(a => Math.Max(0, a.Reviews)),
            activity.Count(a => a.Xp > 0 && a.Date <= today),
            StreakCalculator.Calculate(activity, today),
            calendar);
    }
}
