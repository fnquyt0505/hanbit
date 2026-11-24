using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers;

/// <summary>Trang ôn tập SRS — thẻ nằm ở localStorage nên trang render khung, review-session.js tải thẻ đến hạn.</summary>
[Route("review")]
public sealed class ReviewController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View();
}
