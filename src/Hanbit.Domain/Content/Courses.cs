namespace Hanbit.Domain.Content;

/// <summary>Một bài học (content/courses/{course}/lessons/{slug}.json).</summary>
/// <param name="VocabIds">Id các từ vựng dùng trong bài (chỉ từ <c>reviewed</c>).</param>
/// <param name="Exercises">Bài tập tự soạn; bài tập sinh tự động được thêm ở ExerciseGenerator.</param>
public sealed record Lesson(
    string Slug,
    string Title,
    int Order,
    IReadOnlyList<string> VocabIds,
    IReadOnlyList<string> GrammarIds,
    IReadOnlyList<Exercise> Exercises);

/// <summary>Một khóa học (content/courses/{course}/course.json).</summary>
public sealed record Course(
    string Slug,
    string Title,
    VocabGrade Level,
    string Description,
    IReadOnlyList<LessonSummary> Lessons);

/// <summary>Thông tin tóm tắt bài học trong mục lục khóa học.</summary>
public sealed record LessonSummary(string Slug, string Title, int Order);
