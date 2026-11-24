using Hanbit.Infrastructure.Content;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hanbit.Web.Controllers;

/// <summary>Một tệp dữ liệu tải về được (CC BY-SA: chia sẻ dữ liệu dẫn xuất cùng giấy phép).</summary>
/// <param name="RelativePath">Đường dẫn trong content/.</param>
public sealed record DataDownload(string Name, string RelativePath, string ContentType, string Description);

/// <summary>Trang nguồn dữ liệu &amp; giấy phép — bắt buộc theo CC BY-SA 2.0 KR (RULES: không được xóa/ẩn).</summary>
[Route("nguon-du-lieu")]
public sealed class CreditsController(IOptions<ContentOptions> contentOptions) : Controller
{
    /// <summary>
    /// Danh sách CỐ ĐỊNH tệp được phép tải (không nhận đường dẫn tùy ý → không thể đọc file khác — chống path traversal).
    /// </summary>
    public static IReadOnlyList<DataDownload> Downloads { get; } =
    [
        new("krdict-beginner.json", "vocab/krdict-beginner.json", "application/json",
            "Từ vựng sơ cấp lấy từ 한국어기초사전 (đã lọc, chuẩn hóa) — CC BY-SA 2.0 KR"),
        new("vocab-reviews.json", "vocab-reviews.json", "application/json",
            "Bản duyệt/sửa nghĩa tiếng Việt của Hanbit (dữ liệu dẫn xuất) — CC BY-SA 2.0 KR"),
        new("LICENSE-DATA.md", "LICENSE-DATA.md", "text/markdown",
            "Giấy phép dữ liệu nội dung của Hanbit"),
    ];

    [HttpGet("")]
    public IActionResult Index() => View(Downloads);

    /// <summary><c>/nguon-du-lieu/tai-xuong/{name}</c> — tải tệp dữ liệu (chỉ tên trong <see cref="Downloads"/>).</summary>
    [HttpGet("tai-xuong/{name}")]
    [ResponseCache(Duration = 3600)]
    public IActionResult Download(string name)
    {
        var download = Downloads.FirstOrDefault(d => string.Equals(d.Name, name, StringComparison.Ordinal));
        if (download is null)
        {
            return NotFound();
        }

        var path = Path.Combine(contentOptions.Value.RootPath, download.RelativePath);
        return System.IO.File.Exists(path)
            ? PhysicalFile(path, $"{download.ContentType}; charset=utf-8", download.Name)
            : NotFound();
    }
}
