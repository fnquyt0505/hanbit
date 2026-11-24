namespace Hanbit.Web.Models;

/// <summary>
/// Dữ liệu dashboard phần server biết (khóa học). Tiến độ cá nhân (streak, XP, thẻ đến hạn, bài kế tiếp)
/// nằm ở localStorage → trang render skeleton, JS điền sau (giai đoạn 5).
/// </summary>
public sealed record DashboardViewModel(IReadOnlyList<DashboardCourse> Courses);

/// <param name="FirstLessonHref">Bài đầu tiên của khóa — đích mặc định của "Tiếp tục học" khi chưa có tiến độ.</param>
public sealed record DashboardCourse(string Slug, string Title, string Description, int LessonCount, string? FirstLessonHref)
{
    public string Href => $"/learn/{Slug}";
}
