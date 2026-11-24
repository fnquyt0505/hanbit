namespace Hanbit.Web.Hosting;

/// <summary>Thông tin site (appsettings mục <c>Site</c>) — dùng cho canonical, Open Graph, sitemap.</summary>
public sealed class SiteOptions
{
    public const string SectionName = "Site";

    /// <summary>URL gốc công khai, VD <c>https://hanbit.onrender.com</c>. Trống → lấy từ request (môi trường dev/test).</summary>
    public string? BaseUrl { get; set; }

    /// <summary>URL gốc không có "/" cuối.</summary>
    public string ResolveBaseUrl(HttpRequest request) =>
        (string.IsNullOrWhiteSpace(BaseUrl) ? $"{request.Scheme}://{request.Host}" : BaseUrl).TrimEnd('/');
}
