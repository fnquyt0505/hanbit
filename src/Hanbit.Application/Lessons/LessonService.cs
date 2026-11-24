using Hanbit.Application.Abstractions;
using Hanbit.Domain.Content;
using Hanbit.Domain.Learning;

namespace Hanbit.Application.Lessons;

/// <summary>Toàn bộ nội dung một bài học (CÓ đáp án — chỉ dùng ở server).</summary>
/// <param name="Vocab">Từ vựng theo thứ tự <c>vocab_ids</c>, chỉ từ đã duyệt.</param>
/// <param name="Exercises">Bài tự soạn trước, rồi bài sinh từ từ vựng (<see cref="ExerciseGenerator"/>).</param>
public sealed record LessonContent(
    Course Course,
    Lesson Lesson,
    IReadOnlyList<VocabItem> Vocab,
    IReadOnlyList<GrammarPoint> Grammar,
    IReadOnlyList<Exercise> Exercises)
{
    /// <summary>Bài kế tiếp trong khóa (null nếu là bài cuối).</summary>
    public LessonSummary? Next => Course.Lessons
        .Where(l => l.Order > Lesson.Order)
        .OrderBy(l => l.Order)
        .FirstOrDefault();
}

/// <summary>Đọc bài học và chấm bài tập — dùng chung cho trang /learn/{c}/{l} và API /api/lessons, /api/exercises.</summary>
public sealed class LessonService(IContentRepository contentRepository)
{
    /// <summary>Nội dung bài học; khóa/bài không tồn tại hoặc bài không thuộc mục lục khóa → null.</summary>
    public async Task<LessonContent?> GetAsync(string courseSlug, string lessonSlug, CancellationToken cancellationToken)
    {
        var course = await contentRepository.GetCourseAsync(courseSlug, cancellationToken);
        if (course is null || course.Lessons.All(l => l.Slug != lessonSlug))
        {
            return null;
        }

        var lesson = await contentRepository.GetLessonAsync(courseSlug, lessonSlug, cancellationToken);
        if (lesson is null)
        {
            return null;
        }

        var vocabById = (await contentRepository.GetVocabularyAsync(lesson.VocabIds, cancellationToken))
            .Where(v => v.Reviewed)
            .ToDictionary(v => v.Id, StringComparer.Ordinal);
        var vocab = lesson.VocabIds.Where(vocabById.ContainsKey).Select(id => vocabById[id]).ToList();

        var grammarById = (await contentRepository.GetGrammarAsync(lesson.GrammarIds, cancellationToken))
            .ToDictionary(g => g.Id, StringComparer.Ordinal);
        var grammar = lesson.GrammarIds.Where(grammarById.ContainsKey).Select(id => grammarById[id]).ToList();

        var generated = ExerciseGenerator.Generate(courseSlug, lessonSlug, vocab);
        return new LessonContent(course, lesson, vocab, grammar, [.. lesson.Exercises, .. generated]);
    }

    /// <summary>Chấm một bài tập; bài/bài tập không tồn tại → <see cref="CheckOutcome.NotFound"/>, sai loại câu trả lời → <see cref="CheckOutcome.InvalidAnswer"/>.</summary>
    public async Task<(CheckOutcome Outcome, CheckResult? Result)> CheckAsync(
        string courseSlug, string lessonSlug, string exerciseId, ExerciseAnswer answer, CancellationToken cancellationToken)
    {
        var content = await GetAsync(courseSlug, lessonSlug, cancellationToken);
        var exercise = content?.Exercises.FirstOrDefault(e => e.Id == exerciseId);
        if (exercise is null)
        {
            return (CheckOutcome.NotFound, null);
        }

        var result = AnswerChecker.Check(exercise, answer);
        return result is null ? (CheckOutcome.InvalidAnswer, null) : (CheckOutcome.Checked, result);
    }
}

public enum CheckOutcome
{
    Checked,
    NotFound,
    InvalidAnswer,
}
