using System.Security.Cryptography;
using System.Text;

namespace Hanbit.Web.Hosting;

/// <summary>
/// Script nội tuyến duy nhất của site — CSP cho phép bằng mã băm SHA-256 (không dùng 'unsafe-inline' cho script).
/// Sửa nội dung ở đây (layout render đúng chuỗi này), mã băm tự tính lại.
/// </summary>
public static class InlineScripts
{
    /// <summary>
    /// Áp chế độ sáng/tối đã lưu TRƯỚC khi vẽ (tránh nháy). Chỉ ĐỌC khóa do progress-store.js ghi — ngoại lệ đã chốt 2026-11-23.
    /// </summary>
    public const string ThemeInit =
        "try{var t=JSON.parse(localStorage.getItem('hanbit.settings.v1')||'{}').theme;if(t==='light'||t==='dark')document.documentElement.dataset.theme=t}catch(e){}";

    /// <summary>Nguồn CSP <c>'sha256-…'</c> của <see cref="ThemeInit"/>.</summary>
    public static string ThemeInitHash { get; } = $"'sha256-{Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(ThemeInit)))}'";
}
