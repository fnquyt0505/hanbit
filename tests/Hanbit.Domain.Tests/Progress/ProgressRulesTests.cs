using Hanbit.Domain.Progress;

using Shouldly;

namespace Hanbit.Domain.Tests.Progress;

public class XpRulesTests
{
    [Theory]
    [InlineData(0, 10)]
    [InlineData(10, 20)]
    [InlineData(14, 24)]
    [InlineData(-3, 10)]
    public void ForLesson_TenPlusOnePerCorrect(int correct, int expected)
    {
        XpRules.ForLesson(correct).ShouldBe(expected);
    }

    [Theory]
    [InlineData(0, 5)]
    [InlineData(10, 15)]
    public void ForHangulQuiz_FivePlusOnePerCorrect(int correct, int expected)
    {
        XpRules.ForHangulQuiz(correct).ShouldBe(expected);
    }

    [Theory]
    [InlineData(12, 12)]
    [InlineData(-1, 0)]
    public void ForReviews_OnePerCard(int cards, int expected)
    {
        XpRules.ForReviews(cards).ShouldBe(expected);
    }
}

public class StreakCalculatorTests
{
    private static readonly DateOnly Today = new(2026, 11, 24);

    private static DailyActivity Day(int daysAgo, int xp = 10) => new(Today.AddDays(-daysAgo), xp, 0);

    [Fact]
    public void Calculate_NoActivity_IsZero()
    {
        StreakCalculator.Calculate([], Today).ShouldBe(new Streak(0, 0, false));
    }

    [Fact]
    public void Calculate_ConsecutiveDaysEndingToday_CountsAll()
    {
        StreakCalculator.Calculate([Day(0), Day(1), Day(2)], Today).ShouldBe(new Streak(3, 3, true));
    }

    [Fact]
    public void Calculate_NotYetToday_ButYesterday_KeepsStreak()
    {
        StreakCalculator.Calculate([Day(1), Day(2)], Today).ShouldBe(new Streak(2, 2, false));
    }

    [Fact]
    public void Calculate_MissedYesterday_ResetsCurrent_KeepsLongest()
    {
        StreakCalculator.Calculate([Day(2), Day(3), Day(4), Day(5)], Today).ShouldBe(new Streak(0, 4, false));
    }

    [Fact]
    public void Calculate_GapInMiddle_CurrentOnlyAfterGap()
    {
        StreakCalculator.Calculate([Day(0), Day(1), Day(3), Day(4), Day(5)], Today).ShouldBe(new Streak(2, 3, true));
    }

    [Fact]
    public void Calculate_DayWithZeroXp_DoesNotCount()
    {
        StreakCalculator.Calculate([Day(0, xp: 0), Day(1)], Today).ShouldBe(new Streak(1, 1, false));
    }

    [Fact]
    public void Calculate_FutureDates_Ignored()
    {
        StreakCalculator.Calculate([Day(-1), Day(0)], Today).ShouldBe(new Streak(1, 1, true));
    }

    [Fact]
    public void AddActivity_NewDay_Appends_SameDay_Accumulates()
    {
        var once = StreakCalculator.AddActivity([Day(1, xp: 5)], Today, xp: 20, reviews: 0);
        var twice = StreakCalculator.AddActivity(once, Today, xp: 3, reviews: 3);

        twice.ShouldBe([new DailyActivity(Today.AddDays(-1), 5, 0), new DailyActivity(Today, 23, 3)]);
    }

    [Fact]
    public void AddActivity_TrimsToLast365Days_AndMergesDuplicates()
    {
        var activity = new[] { Day(400), Day(365), Day(364), Day(3, xp: 1), Day(3, xp: 2) };

        var result = StreakCalculator.AddActivity(activity, Today, xp: 1, reviews: 0);

        result.Select(a => a.Date).ShouldBe([Today.AddDays(-364), Today.AddDays(-3), Today]);
        result.Single(a => a.Date == Today.AddDays(-3)).Xp.ShouldBe(3);
    }

    [Fact]
    public void AddActivity_NegativeValues_Clamped()
    {
        StreakCalculator.AddActivity([], Today, xp: -5, reviews: -1).ShouldBe([new DailyActivity(Today, 0, 0)]);
    }
}

public class VietnamTimeTests
{
    [Theory]
    [InlineData("2026-11-24T16:59:59+00:00", "2026-11-24")] // 23:59:59 giờ VN
    [InlineData("2026-11-24T17:00:00+00:00", "2026-11-25")] // qua nửa đêm giờ VN
    [InlineData("2026-11-25T06:30:00+07:00", "2026-11-25")]
    [InlineData("2026-12-31T20:00:00-05:00", "2027-01-01")]
    public void ToDate_UsesVietnamMidnight(string instant, string expected)
    {
        VietnamTime.ToDate(DateTimeOffset.Parse(instant, System.Globalization.CultureInfo.InvariantCulture))
            .ShouldBe(DateOnly.Parse(expected, System.Globalization.CultureInfo.InvariantCulture));
    }

    [Fact]
    public void StartOfDay_IsMidnightPlus7()
    {
        VietnamTime.StartOfDay(new DateOnly(2026, 11, 25)).ShouldBe(new DateTimeOffset(2026, 11, 24, 17, 0, 0, TimeSpan.Zero));
    }

    [Fact]
    public void EmptyProgress_HasCurrentSchema_AndNoData()
    {
        var now = DateTimeOffset.UnixEpoch;
        var progress = LearnerProgress.Empty(now);

        progress.SchemaVersion.ShouldBe(LearnerProgress.CurrentSchemaVersion);
        progress.CreatedAt.ShouldBe(now);
        progress.Lessons.ShouldBeEmpty();
        progress.Cards.ShouldBeEmpty();
        progress.Activity.ShouldBeEmpty();
        progress.TotalXp.ShouldBe(0);
        LearnerProgress.LessonKey("beginner", "chao-hoi").ShouldBe("beginner/chao-hoi");
    }
}
