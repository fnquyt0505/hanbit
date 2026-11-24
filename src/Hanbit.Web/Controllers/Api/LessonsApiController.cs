using Hanbit.Application.Lessons;
using Hanbit.Domain.Progress;
using Hanbit.Web.Models.Learn;
using Hanbit.Web.Models.Progress;

using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers.Api;

/// <summary>API bài học stateless: lấy bài (không đáp án) và chấm từng bài tập.</summary>
[ApiController]
public sealed class LessonsApiController(LessonService lessonService) : ControllerBase
{
    /// <summary><c>GET /api/lessons/{course}/{lesson}</c> → từ vựng, ngữ pháp, bài tập (không đáp án). Không có → 404.</summary>
    [HttpGet("api/lessons/{course}/{lesson}")]
    [ProducesResponseType<LessonDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<LessonDto>> Get(string course, string lesson, CancellationToken cancellationToken)
    {
        var content = await lessonService.GetAsync(course, lesson, cancellationToken);
        return content is null ? NotFound() : LessonDto.From(content);
    }

    /// <summary>
    /// <c>POST /api/lessons/{course}/{lesson}/complete</c> — chấm lại mọi câu trả lời, trả bản ghi bài, thẻ ôn mới,
    /// hoạt động, XP, streak (ARCHITECTURE §4). Bài không có → 404; dữ liệu sai/quá lớn → 400.
    /// </summary>
    [HttpPost("api/lessons/{course}/{lesson}/complete")]
    [ProducesResponseType<CompleteLessonResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CompleteLessonResponse>> Complete(
        string course, string lesson, CompleteLessonRequest request,
        [FromServices] SubmitLessonHandler handler, CancellationToken cancellationToken)
    {
        var answers = request.Answers ?? [];
        var cards = request.Cards ?? [];
        var activity = request.Activity ?? [];
        if (answers.Count > CompleteLessonRequest.MaxAnswers || cards.Count > CompleteLessonRequest.MaxCards || activity.Count > CompleteLessonRequest.MaxActivity)
        {
            ModelState.AddModelError("request", "Dữ liệu gửi lên quá lớn.");
            return ValidationProblem(ModelState);
        }

        var submitted = new List<SubmittedAnswer>();
        foreach (var item in answers)
        {
            if (string.IsNullOrWhiteSpace(item.ExerciseId) || item.Answer?.ToDomain() is not { } answer)
            {
                ModelState.AddModelError(nameof(request.Answers), "Mỗi câu trả lời cần exerciseId và một trong index/pairs/tokens/text.");
                return ValidationProblem(ModelState);
            }

            submitted.Add(new SubmittedAnswer(item.ExerciseId, answer));
        }

        var result = await handler.HandleAsync(
            new SubmitLessonCommand(course, lesson, submitted, request.Previous, cards, activity), cancellationToken);
        if (result is null)
        {
            return NotFound();
        }

        return new CompleteLessonResponse(
            LearnerProgress.LessonKey(course, lesson),
            result.Record,
            result.Cards,
            result.Activity,
            result.XpEarned,
            result.Streak,
            result.Correct,
            result.Total,
            result.Score,
            result.Activity.Count > 0 ? result.Activity[^1].Date : default);
    }

    /// <summary>
    /// <c>POST /api/exercises/check</c> <c>{ course, lesson, exerciseId, answer: { index | pairs | tokens | text } }</c>
    /// → <c>{ correct, correctAnswer, explanationVi }</c>. Bài tập không có → 404; câu trả lời sai dạng → 400.
    /// </summary>
    [HttpPost("api/exercises/check")]
    [ProducesResponseType<CheckExerciseResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<CheckExerciseResponse>> Check(CheckExerciseRequest request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Course) || string.IsNullOrWhiteSpace(request.Lesson) || string.IsNullOrWhiteSpace(request.ExerciseId))
        {
            ModelState.AddModelError("exerciseRef", "Cần course, lesson và exerciseId.");
            return ValidationProblem(ModelState);
        }

        if (request.Answer?.ToDomain() is not { } answer)
        {
            ModelState.AddModelError(nameof(request.Answer), "Cần một câu trả lời: index, pairs, tokens hoặc text.");
            return ValidationProblem(ModelState);
        }

        var (outcome, result) = await lessonService.CheckAsync(request.Course, request.Lesson, request.ExerciseId, answer, cancellationToken);
        switch (outcome)
        {
            case CheckOutcome.NotFound:
                return NotFound();
            case CheckOutcome.InvalidAnswer:
                ModelState.AddModelError(nameof(request.Answer), "Dạng câu trả lời không khớp loại bài tập.");
                return ValidationProblem(ModelState);
            default:
                return new CheckExerciseResponse(result!.Correct, result.CorrectAnswer, result.ExplanationVi,
                    Hanbit.Domain.Learning.ExerciseGenerator.VocabIdFor(request.ExerciseId));
        }
    }
}
