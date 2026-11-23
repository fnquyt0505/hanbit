namespace Hanbit.Infrastructure.Content;

/// <summary>Cấu hình mục <c>Content</c> trong appsettings.</summary>
public sealed class ContentOptions
{
    public const string SectionName = "Content";

    /// <summary>Thư mục content/. Đường dẫn tương đối được Web đổi thành tuyệt đối theo ContentRootPath.</summary>
    public string RootPath { get; set; } = "content";

    /// <summary>Thời gian cache; null = không hết hạn (production: nội dung đóng gói cố định trong image).</summary>
    public TimeSpan? CacheDuration { get; set; }
}
