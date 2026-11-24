using Hanbit.Application.Abstractions;
using Hanbit.Application.Reviews;
using Hanbit.Domain.Progress;
using Hanbit.Domain.Srs;
using Hanbit.Web.Models.Learn;
using Hanbit.Web.Models.Progress;

using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers.Api;

/// <summary><c>POST /api/reviews/due</c>: thẻ hiện có (localStorage) + giới hạn số thẻ trả về.</summary>
public sealed record DueReviewsRequest(IReadOnlyList<ReviewCard>? Cards, int? Limit);

/// <param name="Previews">Khoảng ôn (ngày) nếu chọn từng mức — key again/hard/good/easy.</param>
public sealed record DueReviewDto(ReviewCard Card, VocabDto Word, IReadOnlyDictionary<string, int> Previews);

/// <param name="Total">Tổng số thẻ đến hạn (có thể nhiều hơn số thẻ trả về).</param>
public sealed record DueReviewsResponse(int Total, IReadOnlyList<DueReviewDto> Items, DateOnly Today);

/// <summary><c>POST /api/reviews/grade</c>.</summary>
public sealed record GradeReviewRequest(ReviewCard? Card, ReviewGrade? Grade, IReadOnlyList<DailyActivity>? Activity);

/// <param name="Card">Thẻ sau khi lập lịch — ghi đè vào localStorage.</param>
/// <param name="Activity">Hoạt động mới — thay danh sách cũ.</param>
/// <param name="Previews">Khoảng ôn của từng mức cho thẻ MỚI (dùng khi thẻ "Quên" được ôn lại trong phiên).</param>
public sealed record GradeReviewResponse(ReviewCard Card, IReadOnlyList<DailyActivity> Activity, int Xp, Streak Streak, IReadOnlyDictionary<string, int> Previews);

/// <summary>API ôn tập SRS stateless (ARCHITECTURE §4 — "Ôn tập").</summary>
[ApiController]
[Route("api/reviews")]
public sealed class ReviewsApiController(ReviewService reviewService, IClock clock) : ControllerBase
{
    [HttpPost("due")]
    [ProducesResponseType<DueReviewsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<DueReviewsResponse>> Due(DueReviewsRequest request, CancellationToken cancellationToken)
    {
        var cards = request.Cards ?? [];
        if (cards.Count > CompleteLessonRequest.MaxCards)
        {
            ModelState.AddModelError(nameof(request.Cards), "Dữ liệu gửi lên quá lớn.");
            return ValidationProblem(ModelState);
        }

        var (items, total) = await reviewService.DueAsync(cards, request.Limit ?? ReviewService.DefaultLimit, cancellationToken);
        return new DueReviewsResponse(
            total,
            items.Select(i => new DueReviewDto(
                    i.Card,
                    VocabDto.From(i.Word),
                    PreviewDto(i.Previews)))
                .ToList(),
            clock.Today);
    }

    private static Dictionary<string, int> PreviewDto(IReadOnlyDictionary<ReviewGrade, int> previews) =>
        previews.ToDictionary(p => p.Key.ToString().ToLowerInvariant(), p => p.Value);

    [HttpPost("grade")]
    [ProducesResponseType<GradeReviewResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public ActionResult<GradeReviewResponse> Grade(GradeReviewRequest request)
    {
        var activity = request.Activity ?? [];
        if (request.Card is null || string.IsNullOrWhiteSpace(request.Card.ItemId) || request.Grade is null
            || !Enum.IsDefined(request.Grade.Value) || activity.Count > CompleteLessonRequest.MaxActivity)
        {
            ModelState.AddModelError("request", "Cần thẻ (card) và mức đánh giá (grade: again | hard | good | easy).");
            return ValidationProblem(ModelState);
        }

        var result = reviewService.Grade(request.Card, request.Grade.Value, activity);
        return new GradeReviewResponse(result.Card, result.Activity, result.Xp, result.Streak,
            PreviewDto(Sm2Scheduler.PreviewIntervals(result.Card, clock.UtcNow)));
    }
}
