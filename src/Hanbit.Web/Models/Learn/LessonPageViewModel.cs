using System.Text.Json;

using Hanbit.Application.Lessons;

namespace Hanbit.Web.Models.Learn;

/// <summary>Trang /learn/{course}/{lesson}.</summary>
public sealed record LessonPageViewModel(string CourseTitle, LessonDto Lesson)
{
    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

    public static LessonPageViewModel From(LessonContent content) => new(content.Course.Title, LessonDto.From(content));

    /// <summary>
    /// Dữ liệu cho lesson-runner.js (nhúng trong &lt;script type="application/json"&gt;) — chỉ bài tập KHÔNG đáp án.
    /// Từ vựng/ngữ pháp render HTML ở server; JSON chỉ chứa phần runner cần.
    /// </summary>
    public string RunnerJson => JsonSerializer.Serialize(
        new
        {
            course = Lesson.Course,
            lesson = Lesson.Slug,
            nextSlug = Lesson.NextSlug,
            vocabCount = Lesson.Vocab.Count,
            grammarCount = Lesson.Grammar.Count,
            exercises = Lesson.Exercises,
            vocab = Lesson.Vocab.Select(v => new { v.Id, v.Ko, v.Vi }),
        },
        JsonOptions);
}
