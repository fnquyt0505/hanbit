using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.FileProviders;

namespace Hanbit.Web.Controllers;

/// <summary>
/// PWA offline (giai đoạn 10 — T10.1): trang hiện khi mất mạng mà trang cần mở chưa được lưu, và danh sách file tĩnh
/// (vỏ app) service worker lưu sẵn khi cài — để trang đã lưu vẫn chạy được (kể cả module JS được import không kèm <c>?v=</c>).
/// </summary>
public sealed class OfflineController(IWebHostEnvironment environment) : Controller
{
    /// <summary>Thư mục trong wwwroot thuộc vỏ app.</summary>
    public static readonly IReadOnlyList<string> ShellFolders = ["css", "js", "lib"];

    /// <summary>File lẻ của vỏ app (icon, manifest).</summary>
    public static readonly IReadOnlyList<string> ShellFiles =
    [
        "/manifest.webmanifest",
        "/favicon.svg",
        "/img/icon-192.png",
        "/img/badge-96.png",
    ];

    [HttpGet("/offline")]
    public IActionResult Index() => View();

    /// <summary><c>GET /offline/assets.json</c> — đường dẫn các file JS/CSS/thư viện + icon (không kèm query).</summary>
    [HttpGet("/offline/assets.json")]
    public IActionResult Assets()
    {
        var files = new List<string>();
        foreach (var folder in ShellFolders)
        {
            Collect(environment.WebRootFileProvider, folder, files);
        }

        Response.Headers.CacheControl = "no-cache";
        return Json(new { assets = files.Order(StringComparer.Ordinal).Concat(ShellFiles).ToList() });
    }

    private static void Collect(IFileProvider provider, string path, List<string> files)
    {
        foreach (var entry in provider.GetDirectoryContents(path))
        {
            var child = $"{path}/{entry.Name}";
            if (entry.IsDirectory)
            {
                Collect(provider, child, files);
            }
            else if (entry.Name.EndsWith(".js", StringComparison.OrdinalIgnoreCase) || entry.Name.EndsWith(".css", StringComparison.OrdinalIgnoreCase))
            {
                files.Add($"/{child}");
            }
        }
    }
}
