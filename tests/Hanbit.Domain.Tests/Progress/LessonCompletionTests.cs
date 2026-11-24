using Hanbit.Domain.Progress;

using Shouldly;

namespace Hanbit.Domain.Tests.Progress;

public class LessonCompletionTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero); // 17:00 giờ VN
    private static readonly DateOnly Today = new(2026, 11, 24);
    private static readonly DateTimeOffset TomorrowVn = new(2026, 11, 24, 17, 0, 0, TimeSpan.Zero); // 00:00 25/11 giờ VN

    private static LessonCompletionResult Complete(
        int correct = 10, int total = 14, LessonRecord? previous = null,
        IReadOnlySet<string>? wrong = null, IReadOnlyCollection<ReviewCard>? cards = null, IReadOnlyList<DailyActivity>? activity = null) =>
        LessonCompletion.Complete(previous, correct, total, ["kd-1", "kd-2", "kd-3"], wrong ?? new HashSet<string>(),
            cards ?? [], activity ?? [], Now, Today);

    [Fact]
    public void Complete_FirstTime_CompletedWithScoreAndXp()
    {
        var result = Complete(correct: 7, total: 14);

        result.Record.ShouldBe(new LessonRecord(LessonStatus.Completed, 50, Now));
        result.Score.ShouldBe(50);
        result.XpEarned.ShouldBe(17);
        result.Activity.ShouldBe([new DailyActivity(Today, 17, 0)]);
        result.Streak.ShouldBe(new Streak(1, 1, true));
    }

    [Fact]
    public void Complete_LowerScoreThanBefore_KeepsBestScore()
    {
        Complete(correct: 2, previous: new LessonRecord(LessonStatus.Completed, 90, Now.AddDays(-3))).Record.BestScore.ShouldBe(90);
    }

    [Fact]
    public void Complete_EveryWordBecomesCard_WrongDueNow_OthersDueTomorrowVietnam()
    {
        var result = Complete(wrong: new HashSet<string> { "kd-2" });

        result.Cards.Select(c => c.ItemId).ShouldBe(["kd-1", "kd-2", "kd-3"]);
        result.Cards.Single(c => c.ItemId == "kd-2").DueAt.ShouldBe(Now);
        result.Cards.Where(c => c.ItemId != "kd-2").ShouldAllBe(c => c.DueAt == TomorrowVn);
        result.Cards.ShouldAllBe(c => c.ItemType == ItemType.Vocab && c.Ease == ReviewCard.DefaultEase && c.Repetitions == 0 && c.LastReviewedAt == null);
    }

    [Fact]
    public void Complete_ExistingCards_NotReset_WrongOnesBroughtDueNow()
    {
        var later = Now.AddDays(5);
        var existing = new[]
        {
            new ReviewCard("kd-1", ItemType.Vocab, 2.8, 6, 3, later, Now.AddDays(-1)),
            new ReviewCard("kd-2", ItemType.Vocab, 2.6, 3, 2, later, Now.AddDays(-1)),
        };

        var result = Complete(wrong: new HashSet<string> { "kd-2" }, cards: existing);

        result.Cards.Select(c => c.ItemId).ShouldBe(["kd-2", "kd-3"]);
        result.Cards[0].ShouldBe(existing[1] with { DueAt = Now });
    }

    [Fact]
    public void Complete_AddsToExistingActivity_ContinuesStreak()
    {
        var result = Complete(correct: 0, activity: [new DailyActivity(Today.AddDays(-1), 20, 0), new DailyActivity(Today, 5, 2)]);

        result.Activity.Last().ShouldBe(new DailyActivity(Today, 15, 2));
        result.Streak.Current.ShouldBe(2);
    }

    [Theory]
    [InlineData(20, 14, 100, 24)]
    [InlineData(-5, 14, 0, 10)]
    [InlineData(0, 0, 0, 10)]
    public void Complete_ClampsCorrectToRange(int correct, int total, int score, int xp)
    {
        var result = Complete(correct: correct, total: total);

        result.Score.ShouldBe(score);
        result.XpEarned.ShouldBe(xp);
    }
}
