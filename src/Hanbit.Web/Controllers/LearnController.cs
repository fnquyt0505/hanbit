using Hanbit.Application.Abstractions;
using Hanbit.Web.Models.Learn;

using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers;

[Route("learn")]
public sealed class LearnController(IContentRepository contentRepository) : Controller
{
    /// <summary>Lộ trình một khóa: <c>/learn/{course}</c>. Khóa không tồn tại → 404 (trang lỗi thân thiện).</summary>
    [HttpGet("{course}")]
    public async Task<IActionResult> Course(string course, CancellationToken cancellationToken)
    {
        var model = await contentRepository.GetCourseAsync(course, cancellationToken);
        if (model is null)
        {
            return NotFound();
        }

        return View(new CoursePageViewModel(model));
    }
}
