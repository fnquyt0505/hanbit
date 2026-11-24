using Hanbit.Application.Abstractions;
using Hanbit.Web.Models.Hangul;

using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers;

[Route("hangul")]
public sealed class HangulController(IContentRepository contentRepository) : Controller
{
    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var letters = await contentRepository.GetHangulAsync(cancellationToken);
        return View(HangulIndexViewModel.Create(letters));
    }
}
