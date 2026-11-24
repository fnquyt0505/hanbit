using Hanbit.Domain.Progress;

namespace Hanbit.Web.Models.Progress;

/// <summary><c>POST /api/progress/summary</c>: phần tiến độ cần để tính số liệu dashboard.</summary>
public sealed record ProgressSummaryRequest(
    IReadOnlyDictionary<string, LessonRecord>? Lessons,
    IReadOnlyList<ReviewCard>? Cards,
    IReadOnlyList<DailyActivity>? Activity,
    int TotalXp);

public sealed record NextLessonDto(string Course, string Slug, string Title, int Order, string Href);

/// <param name="NextLesson">Bài đầu tiên chưa hoàn thành (null nếu đã xong hết).</param>
/// <param name="IsNewLearner">Chưa hoàn thành bài/nhóm nào và chưa có XP → gợi ý học Hangul trước.</param>
public sealed record ProgressSummaryResponse(
    Streak Streak,
    int XpToday,
    int TotalXp,
    int DueCards,
    int LearnedWords,
    int CompletedLessons,
    NextLessonDto? NextLesson,
    bool IsNewLearner,
    DateOnly Today);

/// <summary>Một câu trả lời quiz Hangul (dạng câu kebab-case như đề).</summary>
public sealed record HangulQuizAnswerDto(string? Kind, string? Prompt, string? Answer);

/// <summary><c>POST /api/hangul/quiz/complete</c>.</summary>
public sealed record HangulQuizCompleteRequest(
    string? Group,
    IReadOnlyList<HangulQuizAnswerDto>? Answers,
    LessonRecord? Previous,
    IReadOnlyList<DailyActivity>? Activity)
{
    public const int MaxAnswers = 20;
}
