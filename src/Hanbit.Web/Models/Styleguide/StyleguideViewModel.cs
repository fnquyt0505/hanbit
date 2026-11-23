namespace Hanbit.Web.Models.Styleguide;

/// <param name="Name">Tên biến CSS, VD <c>--primary</c>.</param>
/// <param name="Usage">Dùng cho (theo docs/DESIGN.md §2).</param>
/// <param name="TextSafe">Đạt tương phản ≥ 4.5:1 khi làm màu chữ trên nền <c>--bg</c> ở cả sáng lẫn tối.</param>
public sealed record ColorTokenViewModel(string Name, string Usage, bool TextSafe);

public sealed record StyleguideViewModel(IReadOnlyList<ColorTokenViewModel> Colors)
{
    public static StyleguideViewModel Create() => new(
    [
        new("--bg", "Nền trang", TextSafe: false),
        new("--surface", "Card, modal", TextSafe: false),
        new("--ink", "Chữ chính", TextSafe: true),
        new("--ink-muted", "Chữ phụ, romanization", TextSafe: true),
        new("--border", "Viền", TextSafe: false),
        new("--primary", "Nút chính, CTA, streak", TextSafe: true),
        new("--secondary", "Link, tiến độ, thông tin", TextSafe: true),
        new("--success", "Đáp án đúng", TextSafe: false),
        new("--warning", "\"Khó\", sắp hết hạn", TextSafe: false),
        new("--danger", "Đáp án sai", TextSafe: true),
        new("--xp", "Điểm XP, huy hiệu", TextSafe: false),
    ]);
}
