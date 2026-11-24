namespace Hanbit.Domain.Progress;

/// <summary>
/// Tiến độ người học chế độ khách — lưu ở localStorage (<c>hanbit.progress.v1</c>), server chỉ xử lý (ARCHITECTURE §6).
/// Đổi cấu trúc → tăng <see cref="CurrentSchemaVersion"/> + hàm migrate + test (RULES).
/// </summary>
/// <param name="Lessons">Key <c>"course/lesson"</c>.</param>
/// <param name="Cards">Thẻ ôn tập, key = id mục (VD <c>kd-123</c>).</param>
/// <param name="Activity">Hoạt động theo ngày (giờ Việt Nam), tối đa <see cref="ActivityHistoryDays"/> ngày gần nhất.</param>
public sealed record LearnerProgress(
    int SchemaVersion,
    DateTimeOffset CreatedAt,
    IReadOnlyDictionary<string, LessonRecord> Lessons,
    IReadOnlyDictionary<string, ReviewCard> Cards,
    IReadOnlyList<DailyActivity> Activity,
    int TotalXp)
{
    public const int CurrentSchemaVersion = 1;
    public const int ActivityHistoryDays = 365;

    public static LearnerProgress Empty(DateTimeOffset now) => new(
        CurrentSchemaVersion,
        now,
        new Dictionary<string, LessonRecord>(StringComparer.Ordinal),
        new Dictionary<string, ReviewCard>(StringComparer.Ordinal),
        [],
        0);

    public static string LessonKey(string course, string lesson) => $"{course}/{lesson}";
}

public enum LessonStatus
{
    InProgress,
    Completed,
}

/// <param name="BestScore">Điểm cao nhất (0–100%).</param>
public sealed record LessonRecord(LessonStatus Status, int BestScore, DateTimeOffset? CompletedAt);

public enum ItemType
{
    Vocab,
    Hangul,
}

/// <summary>Thẻ ôn tập SRS (SM-2 rút gọn — ARCHITECTURE §7).</summary>
public sealed record ReviewCard(
    string ItemId,
    ItemType ItemType,
    double Ease,
    int IntervalDays,
    int Repetitions,
    DateTimeOffset DueAt,
    DateTimeOffset? LastReviewedAt)
{
    public const double DefaultEase = 2.5;
    public const double MinEase = 1.3;
    public const double MaxEase = 5.0;
    public const int MaxIntervalDays = 3650;
}

/// <summary>Hoạt động một ngày (giờ Việt Nam).</summary>
public sealed record DailyActivity(DateOnly Date, int Xp, int Reviews);
