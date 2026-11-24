using Hanbit.Application.Progress;
using Hanbit.Web.Models.Progress;

using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers.Api;

/// <summary>API tiến độ stateless — tiến độ nằm ở localStorage, server chỉ tính (ARCHITECTURE §2).</summary>
[ApiController]
[Route("api/progress")]
public sealed class ProgressApiController(ProgressService progressService) : ControllerBase
{
    /// <summary>
    /// <c>POST /api/progress/summary</c> → streak, XP hôm nay, thẻ đến hạn, số từ đã học, bài kế tiếp.
    /// Streak/thẻ đến hạn tính ở server theo giờ Việt Nam (JS không tự tính — RULES).
    /// </summary>
    [HttpPost("summary")]
    [ProducesResponseType<ProgressSummaryResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProgressSummaryResponse>> Summary(ProgressSummaryRequest request, CancellationToken cancellationToken)
    {
        var lessons = request.Lessons ?? new Dictionary<string, Domain.Progress.LessonRecord>();
        var cards = request.Cards ?? [];
        var activity = request.Activity ?? [];
        if (lessons.Count > CompleteLessonRequest.MaxAnswers * 10 || cards.Count > CompleteLessonRequest.MaxCards || activity.Count > CompleteLessonRequest.MaxActivity)
        {
            ModelState.AddModelError("request", "Dữ liệu gửi lên quá lớn.");
            return ValidationProblem(ModelState);
        }

        var (summary, next, today) = await progressService.SummarizeAsync(lessons, cards, activity, request.TotalXp, cancellationToken);
        return new ProgressSummaryResponse(
            summary.Streak,
            summary.XpToday,
            summary.TotalXp,
            summary.DueCards,
            summary.LearnedWords,
            summary.CompletedLessons,
            next is null ? null : new NextLessonDto(next.Course, next.Slug, next.Title, next.Order, next.Href),
            IsNewLearner: summary.CompletedLessons == 0 && summary.TotalXp == 0,
            today);
    }
}
