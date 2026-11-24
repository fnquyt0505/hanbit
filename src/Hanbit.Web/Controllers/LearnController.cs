using Hanbit.Application.Abstractions;
using Hanbit.Application.Lessons;
using Hanbit.Web.Models.Learn;

using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers;

[Route("learn")]
public sealed class LearnController(IContentRepository contentRepository, LessonService lessonService) : Controller
{
    /// <summary>Màn học một bài: <c>/learn/{course}/{lesson}</c> — từ vựng → ngữ pháp → bài tập. Không có → 404.</summary>
    [HttpGet("{course}/{lesson}")]
    public async Task<IActionResult> Lesson(string course, string lesson, CancellationToken cancellationToken)
    {
        var content = await lessonService.GetAsync(course, lesson, cancellationToken);
        return content is null ? NotFound() : View(LessonPageViewModel.From(content));
    }

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
