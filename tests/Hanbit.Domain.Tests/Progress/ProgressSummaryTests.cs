using Hanbit.Domain.Progress;

using Shouldly;

namespace Hanbit.Domain.Tests.Progress;

public class ProgressSummaryTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 24);

    private static ReviewCard Card(string id, DateTimeOffset due, ItemType type = ItemType.Vocab) =>
        new(id, type, 2.5, 0, 0, due, null);

    [Fact]
    public void Summarize_Empty_AllZero()
    {
        ProgressSummaries.Summarize([], [], new Dictionary<string, LessonRecord>(), 0, Now, Today)
            .ShouldBe(new ProgressSummary(new Streak(0, 0, false), 0, 0, 0, 0, 0));
    }

    [Fact]
    public void Summarize_CountsDueCardsWordsXpTodayAndCompletedLessons()
    {
        var cards = new[]
        {
            Card("kd-1", Now.AddMinutes(-1)),
            Card("kd-2", Now),
            Card("kd-3", Now.AddDays(1)),
            Card("kd-3", Now.AddDays(-9)), // trùng id → chỉ tính một lần
            Card("h-1", Now.AddDays(-1), ItemType.Hangul),
        };
        var lessons = new Dictionary<string, LessonRecord>
        {
            ["beginner/chao-hoi"] = new(LessonStatus.Completed, 80, Now),
            ["beginner/gioi-thieu"] = new(LessonStatus.InProgress, 0, null),
            ["hangul/nguyen-am"] = new(LessonStatus.Completed, 90, Now),
        };
        var activity = new[] { new DailyActivity(Today.AddDays(-1), 20, 0), new DailyActivity(Today, 15, 3) };

        var summary = ProgressSummaries.Summarize(activity, cards, lessons, 35, Now, Today);

        summary.ShouldBe(new ProgressSummary(new Streak(2, 2, true), 15, 35, 3, 3, 2));
    }

    [Fact]
    public void CompleteHangulQuiz_FivePlusCorrect_NoCards_KeepsBest()
    {
        var result = LessonCompletion.CompleteHangulQuiz(new LessonRecord(LessonStatus.Completed, 90, Now.AddDays(-1)), 7, 10, [], Now, Today);

        result.XpEarned.ShouldBe(12);
        result.Score.ShouldBe(70);
        result.Record.BestScore.ShouldBe(90);
        result.Cards.ShouldBeEmpty();
        result.Activity.ShouldBe([new DailyActivity(Today, 12, 0)]);
        result.Streak.Current.ShouldBe(1);
    }
}
