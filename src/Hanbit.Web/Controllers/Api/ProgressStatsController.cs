using Hanbit.Application.Abstractions;
using Hanbit.Domain.Progress;
using Hanbit.Web.Models.Progress;

using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers.Api;

/// <summary><c>POST /api/progress/stats</c>: thẻ + hoạt động từ localStorage.</summary>
public sealed record ProgressStatsRequest(IReadOnlyList<ReviewCard>? Cards, IReadOnlyList<DailyActivity>? Activity);

/// <summary>Một từ trong danh sách hồ sơ.</summary>
public sealed record WordStatDto(string Id, string Ko, string Vi, WordStatus Status, int IntervalDays, DateTimeOffset DueAt);

/// <param name="Words">Từ đã học, đã thuộc trước rồi theo khoảng ôn giảm dần (tối đa 500).</param>
public sealed record ProgressStatsResponse(
    int MasteredWords,
    int LearningWords,
    int TotalReviews,
    int ActiveDays,
    Streak Streak,
    IReadOnlyList<CalendarDay> Calendar,
    IReadOnlyList<WordStatDto> Words,
    DateOnly Today);

/// <summary>Thống kê trang hồ sơ (T7.1) — server tính, JS hiển thị.</summary>
[ApiController]
public sealed class ProgressStatsController(IContentRepository contentRepository, IClock clock) : ControllerBase
{
    public const int MaxWords = 500;

    [HttpPost("api/progress/stats")]
    [ProducesResponseType<ProgressStatsResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProgressStatsResponse>> Stats(ProgressStatsRequest request, CancellationToken cancellationToken)
    {
        var cards = request.Cards ?? [];
        var activity = request.Activity ?? [];
        if (cards.Count > CompleteLessonRequest.MaxCards || activity.Count > CompleteLessonRequest.MaxActivity)
        {
            ModelState.AddModelError("request", "Dữ liệu gửi lên quá lớn.");
            return ValidationProblem(ModelState);
        }

        var today = clock.Today;
        var stats = ProgressStatsCalculator.Calculate(cards, activity, today);

        var vocabCards = cards
            .Where(c => c.ItemType == ItemType.Vocab)
            .DistinctBy(c => c.ItemId, StringComparer.Ordinal)
            .OrderByDescending(c => c.IntervalDays)
            .ThenBy(c => c.ItemId, StringComparer.Ordinal)
            .Take(MaxWords)
            .ToList();
        var words = (await contentRepository.GetVocabularyAsync(vocabCards.Select(c => c.ItemId), cancellationToken))
            .ToDictionary(w => w.Id, StringComparer.Ordinal);

        return new ProgressStatsResponse(
            stats.MasteredWords,
            stats.LearningWords,
            stats.TotalReviews,
            stats.ActiveDays,
            stats.Streak,
            stats.Calendar,
            vocabCards
                .Where(c => words.ContainsKey(c.ItemId))
                .Select(c => new WordStatDto(c.ItemId, words[c.ItemId].Ko, words[c.ItemId].Vi,
                    ProgressStatsCalculator.StatusOf(c), c.IntervalDays, c.DueAt))
                .ToList(),
            today);
    }
}
