using Hanbit.Domain.Progress;

using Shouldly;

namespace Hanbit.Domain.Tests.Progress;

public class ProgressSanitizerTests
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 24);

    private static ReviewCard Card(string id, double ease = 2.5, int interval = 3, int reps = 1) =>
        new(id, ItemType.Vocab, ease, interval, reps, Now.AddDays(1), Now.AddDays(-1));

    private static LearnerProgress Progress(
        IReadOnlyDictionary<string, LessonRecord>? lessons = null,
        IReadOnlyDictionary<string, ReviewCard>? cards = null,
        IReadOnlyList<DailyActivity>? activity = null,
        int totalXp = 50,
        DateTimeOffset? createdAt = null) =>
        new(1, createdAt ?? Now.AddDays(-10),
            lessons ?? new Dictionary<string, LessonRecord> { ["beginner/chao-hoi"] = new(LessonStatus.Completed, 80, Now.AddDays(-1)) },
            cards ?? new Dictionary<string, ReviewCard> { ["kd-1"] = Card("kd-1") },
            activity ?? [new DailyActivity(Today.AddDays(-1), 20, 0), new DailyActivity(Today, 30, 2)],
            totalXp);

    [Fact]
    public void Sanitize_ValidProgress_UnchangedNoWarnings()
    {
        var input = Progress();

        var result = ProgressSanitizer.Sanitize(input, Now, Today);

        result.Warnings.ShouldBeEmpty();
        result.Progress.Lessons.ShouldBe(input.Lessons);
        result.Progress.Cards.ShouldBe(input.Cards);
        result.Progress.Activity.ShouldBe(input.Activity);
        result.Progress.TotalXp.ShouldBe(50);
        result.Progress.CreatedAt.ShouldBe(input.CreatedAt);
    }

    [Theory]
    [InlineData(9.0, 1.0, 5.0)]
    [InlineData(0.5, 1.0, 1.3)]
    [InlineData(double.NaN, 1.0, 2.5)]
    public void Sanitize_ClampsEase(double ease, double _, double expected)
    {
        var result = ProgressSanitizer.Sanitize(Progress(cards: new Dictionary<string, ReviewCard> { ["kd-1"] = Card("kd-1", ease: ease) }), Now, Today);

        result.Progress.Cards["kd-1"].Ease.ShouldBe(expected);
        result.Warnings.ShouldNotBeEmpty();
    }

    [Fact]
    public void Sanitize_ClampsIntervalRepetitionsScoresAndXp()
    {
        var result = ProgressSanitizer.Sanitize(Progress(
            lessons: new Dictionary<string, LessonRecord> { ["beginner/chao-hoi"] = new(LessonStatus.Completed, 250, Now.AddDays(5)) },
            cards: new Dictionary<string, ReviewCard> { ["kd-1"] = Card("kd-1", interval: 99999, reps: -4) },
            totalXp: -10), Now, Today);

        result.Progress.Cards["kd-1"].IntervalDays.ShouldBe(3650);
        result.Progress.Cards["kd-1"].Repetitions.ShouldBe(0);
        result.Progress.Lessons["beginner/chao-hoi"].BestScore.ShouldBe(100);
        result.Progress.Lessons["beginner/chao-hoi"].CompletedAt.ShouldBe(Now);
        result.Progress.TotalXp.ShouldBe(0);
        result.Warnings.Count.ShouldBeGreaterThanOrEqualTo(3);
    }

    [Fact]
    public void Sanitize_DropsInvalidLessonKeysAndCards_ReKeysCardsByItemId()
    {
        var result = ProgressSanitizer.Sanitize(Progress(
            lessons: new Dictionary<string, LessonRecord>
            {
                ["beginner/chao-hoi"] = new(LessonStatus.Completed, 80, Now),
                ["../../etc"] = new(LessonStatus.Completed, 80, Now),
                ["khong-co-dau-gach"] = new(LessonStatus.Completed, 80, Now),
            },
            cards: new Dictionary<string, ReviewCard>
            {
                ["sai-key"] = Card("kd-9"),
                ["rong"] = Card(" "),
                ["kd-9-trung"] = Card("kd-9"),
            }), Now, Today);

        result.Progress.Lessons.Keys.ShouldBe(["beginner/chao-hoi"]);
        result.Progress.Cards.Keys.ShouldBe(["kd-9"]);
        result.Warnings.ShouldContain(w => w.Contains("bài học"));
        result.Warnings.ShouldContain(w => w.Contains("thẻ"));
    }

    [Fact]
    public void Sanitize_Activity_MergesDuplicates_DropsFutureAndOld_ClampsNegative()
    {
        var result = ProgressSanitizer.Sanitize(Progress(activity:
        [
            new DailyActivity(Today.AddDays(1), 99, 0),
            new DailyActivity(Today.AddDays(-400), 5, 0),
            new DailyActivity(Today, 10, 1),
            new DailyActivity(Today, 5, 1),
            new DailyActivity(Today.AddDays(-2), -7, -1),
        ]), Now, Today);

        result.Progress.Activity.ShouldBe([new DailyActivity(Today.AddDays(-2), 0, 0), new DailyActivity(Today, 15, 2)]);
        result.Warnings.ShouldContain(w => w.Contains("hoạt động"));
    }

    [Fact]
    public void Sanitize_FutureOrMissingCreatedAt_ResetToNow()
    {
        ProgressSanitizer.Sanitize(Progress(createdAt: Now.AddYears(1)), Now, Today).Progress.CreatedAt.ShouldBe(Now);
        ProgressSanitizer.Sanitize(Progress(createdAt: default(DateTimeOffset)), Now, Today).Progress.CreatedAt.ShouldBe(Now);
    }
}
