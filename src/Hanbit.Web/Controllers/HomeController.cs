using System.Diagnostics;

using Hanbit.Web.Models;

using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers;

public class HomeController : Controller
{
    public IActionResult Index()
    {
        return View();
    }

    /// <summary>
    /// Trang lỗi theo mã HTTP (gọi lại bởi <c>UseStatusCodePagesWithReExecute</c>). Giữ mã lỗi gốc;
    /// truy cập trực tiếp /loi/{mã} cũng trả đúng mã đó (mã ngoài 400–599 → 404).
    /// </summary>
    [Route("/loi/{statusCode:int}")]
    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult StatusCodePage(int statusCode)
    {
        var code = statusCode is >= 400 and <= 599 ? statusCode : StatusCodes.Status404NotFound;
        Response.StatusCode = code;
        return View("StatusCode", new StatusCodeViewModel(code));
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
