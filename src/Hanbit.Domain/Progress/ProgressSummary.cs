namespace Hanbit.Domain.Progress;

/// <summary>Số liệu dashboard tính từ tiến độ client gửi lên.</summary>
/// <param name="XpToday">XP của ngày hôm nay (giờ VN).</param>
/// <param name="DueCards">Thẻ đến hạn ôn (DueAt ≤ hiện tại).</param>
/// <param name="LearnedWords">Số thẻ từ vựng (từ đã học).</param>
/// <param name="CompletedLessons">Số bài/nhóm đã hoàn thành.</param>
public sealed record ProgressSummary(Streak Streak, int XpToday, int TotalXp, int DueCards, int LearnedWords, int CompletedLessons);

public static class ProgressSummaries
{
    public static ProgressSummary Summarize(
        IReadOnlyList<DailyActivity> activity,
        IEnumerable<ReviewCard> cards,
        IReadOnlyDictionary<string, LessonRecord> lessons,
        int totalXp,
        DateTimeOffset now,
        DateOnly today)
    {
        var cardList = cards.DistinctBy(c => c.ItemId, StringComparer.Ordinal).ToList();
        return new ProgressSummary(
            StreakCalculator.Calculate(activity, today),
            activity.Where(a => a.Date == today).Sum(a => Math.Max(0, a.Xp)),
            Math.Max(0, totalXp),
            cardList.Count(c => c.DueAt <= now),
            cardList.Count(c => c.ItemType == ItemType.Vocab),
            lessons.Values.Count(l => l.Status == LessonStatus.Completed));
    }
}
