using Hanbit.Domain.Progress;

using Shouldly;

namespace Hanbit.Domain.Tests.Progress;

/// <summary>T8.3: gộp tiến độ thiết bị vào tài khoản.</summary>
public class ProgressMergerTests
{
    private static readonly DateTimeOffset T0 = new(2026, 11, 20, 3, 0, 0, TimeSpan.Zero);

    private static LearnerProgress Progress(
        int xp = 0,
        Dictionary<string, LessonRecord>? lessons = null,
        IEnumerable<ReviewCard>? cards = null,
        IReadOnlyList<DailyActivity>? activity = null,
        DateTimeOffset? createdAt = null) =>
        new(1, createdAt ?? T0, lessons ?? [], (cards ?? []).ToDictionary(c => c.ItemId), activity ?? [], xp);

    private static ReviewCard Card(string id, int reps, DateTimeOffset? reviewed, int dueDays = 1) =>
        new(id, ItemType.Vocab, 2.5, dueDays, reps, T0.AddDays(dueDays), reviewed);

    [Fact]
    public void NoServerCopy_ReturnsDeviceCopy()
    {
        var local = Progress(xp: 30);

        ProgressMerger.Merge(null, local, baseXp: 0).ShouldBeSameAs(local);
    }

    [Fact]
    public void GuestData_AllXpIsNew_AddedToAccount()
    {
        var merged = ProgressMerger.Merge(Progress(xp: 100), Progress(xp: 40), baseXp: 0);

        merged.TotalXp.ShouldBe(140);
    }

    [Fact]
    public void Resync_SameDeviceData_DoesNotDoubleCountXp()
    {
        var server = Progress(xp: 140);

        ProgressMerger.Merge(server, Progress(xp: 140), baseXp: 140).TotalXp.ShouldBe(140);
        ProgressMerger.Merge(server, Progress(xp: 155), baseXp: 140).TotalXp.ShouldBe(155);
    }

    [Fact]
    public void OtherDeviceEarnedMeanwhile_BothKept()
    {
        // Máy A đồng bộ lúc 100 XP rồi học thêm 10; máy B đã đẩy server lên 130.
        ProgressMerger.Merge(Progress(xp: 130), Progress(xp: 110), baseXp: 100).TotalXp.ShouldBe(140);
    }

    [Fact]
    public void DeviceXpBelowBase_NeverSubtracts()
    {
        ProgressMerger.Merge(Progress(xp: 50), Progress(xp: 10), baseXp: 40).TotalXp.ShouldBe(50);
        ProgressMerger.Merge(Progress(xp: 50), Progress(xp: 10), baseXp: -99).TotalXp.ShouldBe(60);
    }

    [Fact]
    public void Lessons_CompletedWins_BestScoreMax_EarliestCompletion()
    {
        var server = Progress(lessons: new()
        {
            ["beginner/a"] = new(LessonStatus.Completed, 60, T0.AddDays(2)),
            ["beginner/b"] = new(LessonStatus.InProgress, 0, null),
            ["beginner/only-server"] = new(LessonStatus.Completed, 90, T0),
        });
        var local = Progress(lessons: new()
        {
            ["beginner/a"] = new(LessonStatus.Completed, 80, T0.AddDays(1)),
            ["beginner/b"] = new(LessonStatus.Completed, 70, T0.AddDays(3)),
            ["beginner/only-local"] = new(LessonStatus.InProgress, 10, null),
        });

        var merged = ProgressMerger.Merge(server, local, 0).Lessons;

        merged["beginner/a"].ShouldBe(new LessonRecord(LessonStatus.Completed, 80, T0.AddDays(1)));
        merged["beginner/b"].ShouldBe(new LessonRecord(LessonStatus.Completed, 70, T0.AddDays(3)));
        merged.Keys.ShouldBe(["beginner/a", "beginner/b", "beginner/only-server", "beginner/only-local"], ignoreOrder: true);
    }

    [Fact]
    public void Cards_MostRecentlyReviewedWins_ThenMoreRepetitions()
    {
        var server = Progress(cards:
        [
            Card("kd-1", reps: 3, reviewed: T0.AddDays(5), dueDays: 10),
            Card("kd-2", reps: 1, reviewed: T0, dueDays: 1),
            Card("kd-3", reps: 0, reviewed: null),
            Card("kd-server", reps: 0, reviewed: null),
        ]);
        var local = Progress(cards:
        [
            Card("kd-1", reps: 4, reviewed: T0.AddDays(4), dueDays: 20),
            Card("kd-2", reps: 2, reviewed: T0.AddDays(1), dueDays: 3),
            Card("kd-3", reps: 1, reviewed: null),
            Card("kd-local", reps: 0, reviewed: null),
        ]);

        var merged = ProgressMerger.Merge(server, local, 0).Cards;

        merged["kd-1"].Repetitions.ShouldBe(3); // server ôn gần hơn
        merged["kd-2"].Repetitions.ShouldBe(2); // máy ôn gần hơn
        merged["kd-3"].Repetitions.ShouldBe(1); // chưa ôn cả hai → nhiều lần lặp hơn
        merged.Count.ShouldBe(5);
    }

    [Fact]
    public void Activity_MaxPerDay_SortedAndCapped()
    {
        var day = new DateOnly(2026, 11, 20);
        var server = Progress(activity: [new(day, 20, 3), new(day.AddDays(1), 5, 0)]);
        var local = Progress(activity: [new(day, 15, 7), new(day.AddDays(-1), 9, 1)]);

        var merged = ProgressMerger.Merge(server, local, 0).Activity;

        merged.ShouldBe([new(day.AddDays(-1), 9, 1), new(day, 20, 7), new(day.AddDays(1), 5, 0)]);

        var longHistory = Enumerable.Range(0, 400).Select(i => new DailyActivity(day.AddDays(-i), 1, 0)).ToList();
        var capped = ProgressMerger.Merge(Progress(activity: longHistory), Progress(), 0).Activity;
        capped.Count.ShouldBe(LearnerProgress.ActivityHistoryDays);
        capped[^1].Date.ShouldBe(day);
    }

    [Fact]
    public void Merge_IsIdempotent_ForSameDeviceData()
    {
        var server = Progress(xp: 50, lessons: new() { ["beginner/a"] = new(LessonStatus.Completed, 60, T0) }, cards: [Card("kd-1", 1, T0)]);
        var local = Progress(xp: 70, lessons: new() { ["beginner/b"] = new(LessonStatus.Completed, 90, T0) }, cards: [Card("kd-2", 2, T0)],
            createdAt: T0.AddDays(-3));

        var once = ProgressMerger.Merge(server, local, baseXp: 0);
        var twice = ProgressMerger.Merge(once, local, baseXp: local.TotalXp);

        twice.TotalXp.ShouldBe(once.TotalXp);
        twice.Lessons.ShouldBe(once.Lessons, ignoreOrder: true);
        twice.Cards.ShouldBe(once.Cards, ignoreOrder: true);
        twice.CreatedAt.ShouldBe(T0.AddDays(-3));
    }
}
