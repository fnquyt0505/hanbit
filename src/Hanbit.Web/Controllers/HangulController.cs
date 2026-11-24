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

    /// <summary>Học từng chữ của một nhóm + quiz 10 câu. Nhóm không tồn tại → 404 (trang lỗi thân thiện).</summary>
    [HttpGet("{group}")]
    public async Task<IActionResult> Group(string group, CancellationToken cancellationToken)
    {
        if (HangulGroupInfo.FindBySlug(group) is not { } info)
        {
            return NotFound();
        }

        var letters = await contentRepository.GetHangulAsync(cancellationToken);
        return View(HangulGroupPageViewModel.Create(info, letters));
    }
}
