using Hanbit.Domain.Content;

namespace Hanbit.Web.Models.Learn;

/// <summary>Trang /learn/{course}.</summary>
public sealed record CoursePageViewModel(Course Course);

/// <summary>Model của View Component <c>CourseMap</c>.</summary>
public sealed record CourseMapViewModel(string CourseSlug, IReadOnlyList<CourseMapLesson> Lessons);

/// <param name="DefaultState">Trạng thái khi chưa có tiến độ: bài đầu <c>open</c>, các bài sau <c>locked</c>.
/// JS (course-map.js) điền trạng thái thật từ localStorage ở giai đoạn 5.</param>
public sealed record CourseMapLesson(int Order, string Slug, string Title, int VocabCount, int GrammarCount, int ExerciseCount, string DefaultState)
{
    public string Key(string courseSlug) => $"{courseSlug}/{Slug}";
}
