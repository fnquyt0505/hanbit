using System.Text.RegularExpressions;

namespace Hanbit.Infrastructure.Content;

/// <summary>Tên file/thư mục trong content/ (ARCHITECTURE §3) — dùng chung cho repository và validator.</summary>
public static partial class ContentPaths
{
    public const string Hangul = "hangul.json";
    public const string VocabReviews = "vocab-reviews.json";
    public const string VocabDirectory = "vocab";
    public const string GrammarDirectory = "grammar";
    public const string CoursesDirectory = "courses";
    public const string CourseFile = "course.json";
    public const string LessonsDirectory = "lessons";

    /// <summary>Slug hợp lệ: chữ thường a–z, số, '-' (không có '/', '.', '..' → chặn path traversal).</summary>
    public static bool IsValidSlug(string? slug) => slug is not null && SlugRegex().IsMatch(slug);

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex SlugRegex();
}
