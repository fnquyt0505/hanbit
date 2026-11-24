using Microsoft.AspNetCore.Http;

namespace Hanbit.Web.Models.Shared;

/// <summary>Biểu tượng của mục điều hướng — vẽ bằng SVG nội tuyến trong partial <c>_NavIcon</c>.</summary>
public enum NavIcon
{
    Dashboard,
    Hangul,
    Learn,
    Review,
    Profile,
}

/// <summary>Một mục điều hướng chính.</summary>
/// <param name="Href">Đường dẫn khi bấm.</param>
/// <param name="ActivePrefix">Mục được đánh dấu "đang ở" khi đường dẫn hiện tại bằng hoặc nằm dưới tiền tố này.</param>
public sealed record NavItem(string Label, string Href, string ActivePrefix, NavIcon Icon)
{
    public bool IsActive(PathString path) =>
        path.StartsWithSegments(ActivePrefix, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Các mục điều hướng chính — dùng chung cho <c>_Sidebar</c> (desktop) và <c>_BottomNav</c> (mobile).</summary>
public static class MainNavigation
{
    public static IReadOnlyList<NavItem> Items { get; } =
    [
        new("Tổng quan", "/dashboard", "/dashboard", NavIcon.Dashboard),
        new("Hangul", "/hangul", "/hangul", NavIcon.Hangul),
        new("Bài học", "/learn/beginner", "/learn", NavIcon.Learn),
        new("Ôn tập", "/review", "/review", NavIcon.Review),
        new("Hồ sơ", "/profile", "/profile", NavIcon.Profile),
    ];
}
