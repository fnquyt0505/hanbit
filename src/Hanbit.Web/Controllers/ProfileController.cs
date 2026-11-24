using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers;

/// <summary>Hồ sơ chế độ khách: số liệu + sao lưu/khôi phục tiến độ (T5.5; thống kê chi tiết ở T7.1).</summary>
[Route("profile")]
public sealed class ProfileController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
