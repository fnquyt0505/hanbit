using Hanbit.Domain.Progress;

namespace Hanbit.Domain.Srs;

/// <summary>Tự đánh giá khi ôn thẻ (ARCHITECTURE §7).</summary>
public enum ReviewGrade
{
    Again = 0,
    Hard = 1,
    Good = 2,
    Easy = 3,
}

/// <summary>
/// SM-2 rút gọn (hàm thuần). Quy ước:
/// <list type="bullet">
/// <item>Again: lặp = 0, khoảng = 0, đến hạn NGAY (ôn lại trong phiên), ease −0,20.</item>
/// <item>Hard: 1 ngày ở 2 lần đầu, sau đó khoảng × 1,2; ease −0,15.</item>
/// <item>Good: 1 → 3 ngày → khoảng × ease; ease giữ nguyên.</item>
/// <item>Easy: 3 → 6 ngày → khoảng × ease × 1,3; ease +0,15.</item>
/// </list>
/// Ease trong [1,3; 5], khoảng ≤ 3650 ngày; đến hạn lúc 00:00 giờ Việt Nam của ngày hẹn (ôn được cả ngày).
/// </summary>
public static class Sm2Scheduler
{
    public const double AgainEasePenalty = 0.20;
    public const double HardEasePenalty = 0.15;
    public const double EasyEaseBonus = 0.15;
    public const double HardIntervalFactor = 1.2;
    public const double EasyBonusFactor = 1.3;

    public static ReviewCard Schedule(ReviewCard card, ReviewGrade grade, DateTimeOffset now)
    {
        var today = VietnamTime.ToDate(now);
        if (grade == ReviewGrade.Again)
        {
            return card with
            {
                Ease = ClampEase(card.Ease - AgainEasePenalty),
                IntervalDays = 0,
                Repetitions = 0,
                DueAt = now,
                LastReviewedAt = now,
            };
        }

        var repetitions = card.Repetitions + 1;
        var ease = grade switch
        {
            ReviewGrade.Hard => ClampEase(card.Ease - HardEasePenalty),
            ReviewGrade.Easy => ClampEase(card.Ease + EasyEaseBonus),
            _ => ClampEase(card.Ease),
        };
        var previous = Math.Max(1, card.IntervalDays);
        var interval = (grade, repetitions) switch
        {
            (ReviewGrade.Hard, <= 2) => 1,
            (ReviewGrade.Hard, _) => Round(previous * HardIntervalFactor),
            (ReviewGrade.Good, 1) => 1,
            (ReviewGrade.Good, 2) => 3,
            (ReviewGrade.Good, _) => Round(previous * ease),
            (_, 1) => 3,
            (_, 2) => 6,
            _ => Round(previous * ease * EasyBonusFactor),
        };
        interval = Math.Clamp(interval, 1, ReviewCard.MaxIntervalDays);

        return card with
        {
            Ease = ease,
            IntervalDays = interval,
            Repetitions = repetitions,
            DueAt = VietnamTime.StartOfDay(today.AddDays(interval)),
            LastReviewedAt = now,
        };
    }

    /// <summary>Khoảng ôn (ngày) nếu chọn từng mức — hiển thị trên nút đánh giá (DESIGN.md §5 GradeButtons).</summary>
    public static IReadOnlyDictionary<ReviewGrade, int> PreviewIntervals(ReviewCard card, DateTimeOffset now) =>
        Enum.GetValues<ReviewGrade>().ToDictionary(g => g, g => Schedule(card, g, now).IntervalDays);

    private static double ClampEase(double ease) =>
        double.IsFinite(ease) ? Math.Round(Math.Clamp(ease, ReviewCard.MinEase, ReviewCard.MaxEase), 2) : ReviewCard.DefaultEase;

    private static int Round(double days) => (int)Math.Min(Math.Round(days, MidpointRounding.AwayFromZero), ReviewCard.MaxIntervalDays);
}
