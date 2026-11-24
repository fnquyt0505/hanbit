namespace Hanbit.Domain.Progress;

/// <summary>Chuỗi ngày học liên tiếp.</summary>
/// <param name="Current">Chuỗi hiện tại — vẫn giữ nếu hôm nay chưa học nhưng hôm qua có học.</param>
/// <param name="Longest">Chuỗi dài nhất trong lịch sử.</param>
/// <param name="ActiveToday">Hôm nay đã có XP.</param>
public sealed record Streak(int Current, int Longest, bool ActiveToday);

/// <summary>
/// Tính streak: một ngày "có học" khi có ít nhất 1 XP (người dùng chốt 2026-11-24). Ngày theo giờ Việt Nam
/// (<c>today</c> do server xác định qua IClock — không tin ngày của trình duyệt).
/// </summary>
public static class StreakCalculator
{
    public static Streak Calculate(IEnumerable<DailyActivity> activity, DateOnly today)
    {
        var days = activity.Where(a => a.Xp > 0 && a.Date <= today).Select(a => a.Date).ToHashSet();
        var activeToday = days.Contains(today);

        var current = 0;
        for (var day = activeToday ? today : today.AddDays(-1); days.Contains(day); day = day.AddDays(-1))
        {
            current++;
        }

        var longest = 0;
        var run = 0;
        DateOnly? previous = null;
        foreach (var day in days.Order())
        {
            run = previous is { } p && p.AddDays(1) == day ? run + 1 : 1;
            longest = Math.Max(longest, run);
            previous = day;
        }

        return new Streak(current, longest, activeToday);
    }

    /// <summary>
    /// Cộng XP/lượt ôn vào hoạt động của <paramref name="today"/>; giữ tối đa
    /// <see cref="LearnerProgress.ActivityHistoryDays"/> ngày gần nhất, sắp theo ngày tăng dần.
    /// </summary>
    public static IReadOnlyList<DailyActivity> AddActivity(IEnumerable<DailyActivity> activity, DateOnly today, int xp, int reviews)
    {
        var byDate = activity
            .GroupBy(a => a.Date)
            .ToDictionary(g => g.Key, g => new DailyActivity(g.Key, g.Sum(a => a.Xp), g.Sum(a => a.Reviews)));

        byDate[today] = byDate.TryGetValue(today, out var existing)
            ? existing with { Xp = existing.Xp + Math.Max(0, xp), Reviews = existing.Reviews + Math.Max(0, reviews) }
            : new DailyActivity(today, Math.Max(0, xp), Math.Max(0, reviews));

        var oldest = today.AddDays(-(LearnerProgress.ActivityHistoryDays - 1));
        return byDate.Values.Where(a => a.Date >= oldest && a.Date <= today).OrderBy(a => a.Date).ToList();
    }
}
