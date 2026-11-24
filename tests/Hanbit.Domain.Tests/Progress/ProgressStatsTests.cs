using Hanbit.Domain.Progress;

using Shouldly;

namespace Hanbit.Domain.Tests.Progress;

public class ProgressStatsTests
{
    private static readonly DateOnly Today = new(2026, 11, 26); // thứ Năm
    private static readonly DateTimeOffset Due = new(2026, 12, 1, 0, 0, 0, TimeSpan.Zero);

    private static ReviewCard Card(string id, int interval, ItemType type = ItemType.Vocab) => new(id, type, 2.5, interval, 3, Due, null);

    [Theory]
    [InlineData(0, 0)]
    [InlineData(1, 1)]
    [InlineData(10, 1)]
    [InlineData(11, 2)]
    [InlineData(25, 2)]
    [InlineData(26, 3)]
    [InlineData(50, 3)]
    [InlineData(51, 4)]
    public void LevelOf_BucketsXp(int xp, int level)
    {
        ProgressStatsCalculator.LevelOf(xp).ShouldBe(level);
    }

    [Theory]
    [InlineData(20, WordStatus.Learning)]
    [InlineData(21, WordStatus.Mastered)]
    public void StatusOf_MasteredAtTwentyOneDays(int interval, WordStatus status)
    {
        ProgressStatsCalculator.StatusOf(Card("kd-1", interval)).ShouldBe(status);
    }

    [Fact]
    public void Calculate_CountsWordsReviewsActiveDaysAndStreak()
    {
        var cards = new[] { Card("kd-1", 30), Card("kd-2", 3), Card("kd-3", 21), Card("kd-3", 1), Card("h-1", 40, ItemType.Hangul) };
        var activity = new[]
        {
            new DailyActivity(Today.AddDays(-5), 20, 4),
            new DailyActivity(Today.AddDays(-1), 12, 2),
            new DailyActivity(Today, 30, 6),
        };

        var stats = ProgressStatsCalculator.Calculate(cards, activity, Today);

        stats.MasteredWords.ShouldBe(2);
        stats.LearningWords.ShouldBe(1);
        stats.TotalReviews.ShouldBe(12);
        stats.ActiveDays.ShouldBe(3);
        stats.Streak.ShouldBe(new Streak(2, 2, true));
    }

    [Fact]
    public void Calculate_Calendar_StartsMonday_SeventeenWeeks_EndsToday()
    {
        var stats = ProgressStatsCalculator.Calculate([], [new DailyActivity(Today, 30, 1)], Today);

        stats.Calendar[0].Date.DayOfWeek.ShouldBe(DayOfWeek.Monday);
        stats.Calendar[^1].ShouldBe(new CalendarDay(Today, 30, 1, 3));
        stats.Calendar.Count.ShouldBe((7 * 16) + 4); // 16 tuần đủ + T2→T5 của tuần này
        stats.Calendar.Select(d => d.Date).ShouldBeUnique();
        stats.Calendar.Take(stats.Calendar.Count - 1).ShouldAllBe(d => d.Level == 0);
    }

    [Fact]
    public void Calculate_Calendar_OnSunday_HasFullSeventeenWeeks()
    {
        var sunday = new DateOnly(2026, 11, 29);

        ProgressStatsCalculator.Calculate([], [], sunday).Calendar.Count.ShouldBe(7 * 17);
    }
}
