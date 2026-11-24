using Hanbit.Domain.Progress;
using Hanbit.Domain.Srs;

using Shouldly;

namespace Hanbit.Domain.Tests.Srs;

public class Sm2SchedulerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero); // 17:00 giờ VN 24/11

    private static ReviewCard Card(double ease = 2.5, int interval = 0, int reps = 0) =>
        new("kd-1", ItemType.Vocab, ease, interval, reps, Now, null);

    /// <summary>00:00 giờ VN của (24/11 + n ngày).</summary>
    private static DateTimeOffset DueAfter(int days) => new DateTimeOffset(2026, 11, 23, 17, 0, 0, TimeSpan.Zero).AddDays(days);

    [Fact]
    public void Again_ResetsRepetitions_DueNow_LowersEase()
    {
        var result = Sm2Scheduler.Schedule(Card(ease: 2.5, interval: 10, reps: 4), ReviewGrade.Again, Now);

        result.Repetitions.ShouldBe(0);
        result.IntervalDays.ShouldBe(0);
        result.DueAt.ShouldBe(Now);
        result.Ease.ShouldBe(2.3);
        result.LastReviewedAt.ShouldBe(Now);
    }

    [Theory]
    [InlineData(ReviewGrade.Hard, 1, 2.35)]
    [InlineData(ReviewGrade.Good, 1, 2.5)]
    [InlineData(ReviewGrade.Easy, 3, 2.65)]
    public void NewCard_FirstReview(ReviewGrade grade, int interval, double ease)
    {
        var result = Sm2Scheduler.Schedule(Card(), grade, Now);

        result.Repetitions.ShouldBe(1);
        result.IntervalDays.ShouldBe(interval);
        result.Ease.ShouldBe(ease);
        result.DueAt.ShouldBe(DueAfter(interval));
    }

    [Theory]
    [InlineData(ReviewGrade.Hard, 1)]
    [InlineData(ReviewGrade.Good, 3)]
    [InlineData(ReviewGrade.Easy, 6)]
    public void SecondReview(ReviewGrade grade, int interval)
    {
        Sm2Scheduler.Schedule(Card(interval: 1, reps: 1), grade, Now).IntervalDays.ShouldBe(interval);
    }

    [Theory]
    [InlineData(ReviewGrade.Hard, 12)]  // 10 × 1,2
    [InlineData(ReviewGrade.Good, 25)]  // 10 × 2,5
    [InlineData(ReviewGrade.Easy, 34)]  // 10 × 2,65 × 1,3 = 34,45
    public void MatureCard_MultipliesInterval(ReviewGrade grade, int interval)
    {
        Sm2Scheduler.Schedule(Card(ease: 2.5, interval: 10, reps: 3), grade, Now).IntervalDays.ShouldBe(interval);
    }

    [Fact]
    public void Ease_NeverBelowMinimum()
    {
        var card = Card(ease: 1.35);
        for (var i = 0; i < 5; i++)
        {
            card = Sm2Scheduler.Schedule(card, ReviewGrade.Again, Now);
        }

        card.Ease.ShouldBe(ReviewCard.MinEase);
        Sm2Scheduler.Schedule(Card(ease: 1.3, interval: 10, reps: 5), ReviewGrade.Hard, Now).Ease.ShouldBe(1.3);
    }

    [Fact]
    public void Ease_NeverAboveMaximum()
    {
        Sm2Scheduler.Schedule(Card(ease: 4.95, interval: 10, reps: 5), ReviewGrade.Easy, Now).Ease.ShouldBe(ReviewCard.MaxEase);
    }

    [Fact]
    public void Interval_CappedAt3650Days()
    {
        var result = Sm2Scheduler.Schedule(Card(ease: 5, interval: 3000, reps: 20), ReviewGrade.Easy, Now);

        result.IntervalDays.ShouldBe(ReviewCard.MaxIntervalDays);
    }

    [Fact]
    public void AgainThenGood_StartsOverFromOneDay()
    {
        var lapsed = Sm2Scheduler.Schedule(Card(interval: 30, reps: 6), ReviewGrade.Again, Now);

        Sm2Scheduler.Schedule(lapsed, ReviewGrade.Good, Now).IntervalDays.ShouldBe(1);
    }

    [Fact]
    public void DueAt_IsVietnamMidnight_EvenLateAtNightUtc()
    {
        var lateNow = new DateTimeOffset(2026, 11, 24, 18, 0, 0, TimeSpan.Zero); // 01:00 giờ VN ngày 25/11

        var result = Sm2Scheduler.Schedule(Card(), ReviewGrade.Good, lateNow);

        result.DueAt.ShouldBe(new DateTimeOffset(2026, 11, 25, 17, 0, 0, TimeSpan.Zero)); // 00:00 ngày 26/11 giờ VN
    }

    [Fact]
    public void PreviewIntervals_MatchesScheduleForEachGrade()
    {
        var preview = Sm2Scheduler.PreviewIntervals(Card(interval: 3, reps: 2), Now);

        preview.ShouldBe(new Dictionary<ReviewGrade, int>
        {
            [ReviewGrade.Again] = 0,
            [ReviewGrade.Hard] = 4,
            [ReviewGrade.Good] = 8,
            [ReviewGrade.Easy] = 10,
        });
    }

    [Fact]
    public void Schedule_InvalidEase_FallsBackToDefault()
    {
        Sm2Scheduler.Schedule(Card(ease: double.NaN), ReviewGrade.Good, Now).Ease.ShouldBe(ReviewCard.DefaultEase);
    }
}
