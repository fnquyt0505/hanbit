using Hanbit.Application.Abstractions;
using Hanbit.Domain.Content;
using Hanbit.Web.Models.Learn;

using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.ViewComponents;

/// <summary>
/// CourseMap (DESIGN.md §5): lộ trình bài học dạng danh sách dọc, trạng thái khóa/mở/hoàn thành.
/// Server render trạng thái mặc định (bài 1 mở); <c>wwwroot/js/course-map.js</c> điền theo tiến độ.
/// </summary>
public sealed class CourseMapViewComponent(IContentRepository contentRepository) : ViewComponent
{
    public async Task<IViewComponentResult> InvokeAsync(Course course)
    {
        var lessons = new List<CourseMapLesson>();
        var ordered = course.Lessons.OrderBy(l => l.Order).ToList();
        for (var i = 0; i < ordered.Count; i++)
        {
            var summary = ordered[i];
            var lesson = await contentRepository.GetLessonAsync(course.Slug, summary.Slug, HttpContext.RequestAborted);
            lessons.Add(new CourseMapLesson(
                summary.Order,
                summary.Slug,
                summary.Title,
                lesson?.VocabIds.Count ?? 0,
                lesson?.GrammarIds.Count ?? 0,
                lesson?.Exercises.Count ?? 0,
                i == 0 ? "open" : "locked"));
        }

        return View(new CourseMapViewModel(course.Slug, lessons));
    }
}
