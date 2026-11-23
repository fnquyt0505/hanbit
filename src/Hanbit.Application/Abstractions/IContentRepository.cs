using Hanbit.Domain.Content;

namespace Hanbit.Application.Abstractions;

/// <summary>
/// Truy cập dữ liệu nội dung (content/). Mọi nơi đọc nội dung PHẢI đi qua interface này (RULES §7).
/// Từ vựng trả về đã được gộp với vocab-reviews.json.
/// </summary>
public interface IContentRepository
{
    /// <summary>40 chữ cái Hangul theo thứ tự trong hangul.json.</summary>
    Task<IReadOnlyList<HangulLetter>> GetHangulAsync(CancellationToken cancellationToken);

    /// <summary>Tất cả khóa học, sắp theo slug.</summary>
    Task<IReadOnlyList<Course>> GetCoursesAsync(CancellationToken cancellationToken);

    /// <summary>Khóa học theo slug; null nếu không tồn tại hoặc slug không hợp lệ.</summary>
    Task<Course?> GetCourseAsync(string courseSlug, CancellationToken cancellationToken);

    /// <summary>Bài học theo slug khóa + slug bài; null nếu không tồn tại hoặc slug không hợp lệ.</summary>
    Task<Lesson?> GetLessonAsync(string courseSlug, string lessonSlug, CancellationToken cancellationToken);

    /// <summary>Từ vựng theo id, giữ thứ tự <paramref name="ids"/>; id không tồn tại bị bỏ qua.</summary>
    Task<IReadOnlyList<VocabItem>> GetVocabularyAsync(IEnumerable<string> ids, CancellationToken cancellationToken);

    /// <summary>Ngữ pháp theo id, giữ thứ tự <paramref name="ids"/>; id không tồn tại bị bỏ qua.</summary>
    Task<IReadOnlyList<GrammarPoint>> GetGrammarAsync(IEnumerable<string> ids, CancellationToken cancellationToken);
}
