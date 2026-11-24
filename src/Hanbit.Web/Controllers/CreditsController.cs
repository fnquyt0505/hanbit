using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers;

/// <summary>Trang nguồn dữ liệu &amp; giấy phép — bắt buộc theo CC BY-SA 2.0 KR (RULES: không được xóa/ẩn).</summary>
[Route("nguon-du-lieu")]
public sealed class CreditsController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
