using Hanbit.Application.Abstractions;
using Hanbit.Domain.Content;
using Hanbit.Domain.Progress;
using Hanbit.Domain.Srs;

namespace Hanbit.Application.Reviews;

/// <summary>Một thẻ đến hạn kèm nội dung từ vựng và khoảng ôn tiếp theo của từng mức.</summary>
public sealed record DueReview(ReviewCard Card, VocabItem Word, IReadOnlyDictionary<ReviewGrade, int> Previews);

/// <summary>Kết quả chấm một thẻ.</summary>
public sealed record GradedReview(ReviewCard Card, IReadOnlyList<DailyActivity> Activity, int Xp, Streak Streak);

/// <summary>Ôn tập SRS stateless: thẻ nằm ở localStorage, server lọc thẻ đến hạn và lập lịch (ARCHITECTURE §4).</summary>
public sealed class ReviewService(IContentRepository contentRepository, IClock clock)
{
    public const int DefaultLimit = 50;

    /// <summary>
    /// Thẻ đến hạn (DueAt ≤ hiện tại), sớm nhất trước, tối đa <paramref name="limit"/> thẻ. Chỉ thẻ từ vựng còn trong nội dung
    /// (đã duyệt); thẻ khác bị bỏ qua. <c>Total</c> = tổng thẻ đến hạn (kể cả ngoài giới hạn).
    /// </summary>
    public async Task<(IReadOnlyList<DueReview> Items, int Total)> DueAsync(
        IReadOnlyList<ReviewCard> cards, int limit, CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var due = cards
            .Where(c => c.ItemType == ItemType.Vocab && c.DueAt <= now)
            .DistinctBy(c => c.ItemId, StringComparer.Ordinal)
            .OrderBy(c => c.DueAt)
            .ThenBy(c => c.ItemId, StringComparer.Ordinal)
            .ToList();

        var words = (await contentRepository.GetVocabularyAsync(due.Select(c => c.ItemId), cancellationToken))
            .Where(w => w.Reviewed)
            .ToDictionary(w => w.Id, StringComparer.Ordinal);
        var known = due.Where(c => words.ContainsKey(c.ItemId)).ToList();

        var items = known
            .Take(Math.Clamp(limit, 1, 200))
            .Select(c => new DueReview(c, words[c.ItemId], Sm2Scheduler.PreviewIntervals(c, now)))
            .ToList();
        return (items, known.Count);
    }

    /// <summary>Chấm thẻ: lập lịch SM-2, +1 XP và +1 lượt ôn cho hôm nay (giờ VN), tính lại streak.</summary>
    public GradedReview Grade(ReviewCard card, ReviewGrade grade, IReadOnlyList<DailyActivity> activity)
    {
        var now = clock.UtcNow;
        var today = clock.Today;
        var scheduled = Sm2Scheduler.Schedule(card, grade, now);
        var xp = XpRules.ForReviews(1);
        var newActivity = StreakCalculator.AddActivity(activity, today, xp, reviews: 1);
        return new GradedReview(scheduled, newActivity, xp, StreakCalculator.Calculate(newActivity, today));
    }
}
