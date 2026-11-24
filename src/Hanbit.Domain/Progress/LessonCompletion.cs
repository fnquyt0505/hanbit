namespace Hanbit.Domain.Progress;

/// <summary>Kết quả hoàn thành một bài học — client gộp vào localStorage.</summary>
/// <param name="Cards">Thẻ cần thêm/cập nhật (thẻ mới + thẻ cũ của từ làm sai được đưa về hạn hôm nay).</param>
/// <param name="Activity">Toàn bộ hoạt động sau khi cộng XP hôm nay (client thay thế danh sách cũ).</param>
/// <param name="Score">Điểm % lần này.</param>
public sealed record LessonCompletionResult(
    LessonRecord Record,
    IReadOnlyList<ReviewCard> Cards,
    IReadOnlyList<DailyActivity> Activity,
    int XpEarned,
    Streak Streak,
    int Correct,
    int Total,
    int Score);

/// <summary>
/// Hoàn thành bài học (logic thuần). Quy tắc người dùng chốt 2026-11-24: XP theo số câu đúng; hoàn thành là mở bài sau
/// (không cần điểm); MỌI từ của bài thành thẻ — từ làm sai đến hạn ngay, từ còn lại đến hạn 00:00 ngày mai (giờ VN).
/// </summary>
public static class LessonCompletion
{
    public static LessonCompletionResult Complete(
        LessonRecord? previous,
        int correct,
        int total,
        IReadOnlyList<string> lessonVocabIds,
        IReadOnlySet<string> wrongVocabIds,
        IReadOnlyCollection<ReviewCard> existingCards,
        IReadOnlyList<DailyActivity> activity,
        DateTimeOffset now,
        DateOnly today)
    {
        correct = Math.Clamp(correct, 0, Math.Max(0, total));
        var score = total > 0 ? (int)Math.Round(correct * 100.0 / total) : 0;
        var record = new LessonRecord(LessonStatus.Completed, Math.Max(score, previous?.BestScore ?? 0), now);

        var existing = existingCards
            .GroupBy(c => c.ItemId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.Ordinal);
        var tomorrow = VietnamTime.StartOfDay(today.AddDays(1));
        var cards = new List<ReviewCard>();
        foreach (var id in lessonVocabIds.Distinct(StringComparer.Ordinal))
        {
            var wrong = wrongVocabIds.Contains(id);
            if (existing.TryGetValue(id, out var card))
            {
                if (wrong && card.DueAt > now)
                {
                    cards.Add(card with { DueAt = now });
                }
            }
            else
            {
                cards.Add(new ReviewCard(id, ItemType.Vocab, ReviewCard.DefaultEase, 0, 0, wrong ? now : tomorrow, null));
            }
        }

        var xp = XpRules.ForLesson(correct);
        var newActivity = StreakCalculator.AddActivity(activity, today, xp, reviews: 0);
        return new LessonCompletionResult(record, cards, newActivity, xp, StreakCalculator.Calculate(newActivity, today), correct, total, score);
    }
}
