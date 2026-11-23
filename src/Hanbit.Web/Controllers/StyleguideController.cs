using Hanbit.Web.Models.Styleguide;

using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers;

[Route("styleguide")]
public sealed class StyleguideController : Controller
{
    [HttpGet("")]
    public IActionResult Index() => View(StyleguideViewModel.Create());
}
