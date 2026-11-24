using Hanbit.Application.Abstractions;
using Hanbit.Domain.Content;
using Hanbit.Domain.Hangul;
using Hanbit.Domain.Progress;

namespace Hanbit.Application.Progress;

/// <summary>Bài học tiếp theo gợi ý trên dashboard.</summary>
public sealed record NextLesson(string Course, string Slug, string Title, int Order)
{
    public string Href => $"/learn/{Course}/{Slug}";
}

/// <summary>Câu trả lời quiz Hangul gửi khi hoàn thành.</summary>
public sealed record HangulQuizAnswer(HangulQuizKind Kind, string Prompt, string Answer);

/// <summary>Tóm tắt tiến độ + hoàn thành quiz Hangul (stateless — tiến độ nằm ở client).</summary>
public sealed class ProgressService(IContentRepository contentRepository, IClock clock)
{
    public const string HangulKeyPrefix = "hangul/";

    /// <summary>Số liệu dashboard + bài kế tiếp (bài đầu tiên chưa hoàn thành, theo thứ tự khóa rồi bài).</summary>
    public async Task<(ProgressSummary Summary, NextLesson? Next, DateOnly Today)> SummarizeAsync(
        IReadOnlyDictionary<string, LessonRecord> lessons,
        IReadOnlyList<ReviewCard> cards,
        IReadOnlyList<DailyActivity> activity,
        int totalXp,
        CancellationToken cancellationToken)
    {
        var today = clock.Today;
        var summary = ProgressSummaries.Summarize(activity, cards, lessons, totalXp, clock.UtcNow, today);

        NextLesson? next = null;
        foreach (var course in await contentRepository.GetCoursesAsync(cancellationToken))
        {
            var lesson = course.Lessons
                .OrderBy(l => l.Order)
                .FirstOrDefault(l => !(lessons.TryGetValue(LearnerProgress.LessonKey(course.Slug, l.Slug), out var r) && r.Status == LessonStatus.Completed));
            if (lesson is not null)
            {
                next = new NextLesson(course.Slug, lesson.Slug, lesson.Title, lesson.Order);
                break;
            }
        }

        return (summary, next, today);
    }

    /// <summary>
    /// Hoàn thành quiz Hangul: chấm lại từng câu ở server (<see cref="HangulQuiz.Check"/>), XP 5 + 1/câu đúng.
    /// Nhóm không tồn tại hoặc câu hỏi không thuộc nhóm → null.
    /// </summary>
    public LessonCompletionResult? CompleteHangulQuiz(
        IReadOnlyList<HangulLetter> groupLetters,
        IReadOnlyList<HangulQuizAnswer> answers,
        LessonRecord? previous,
        IReadOnlyList<DailyActivity> activity)
    {
        var correct = 0;
        foreach (var answer in answers)
        {
            var check = HangulQuiz.Check(groupLetters, answer.Kind, answer.Prompt, answer.Answer);
            if (check is null)
            {
                return null;
            }

            if (check.Correct)
            {
                correct++;
            }
        }

        return LessonCompletion.CompleteHangulQuiz(previous, correct, answers.Count, activity, clock.UtcNow, clock.Today);
    }
}
