using System.Text.Json;

using Hanbit.Application.Abstractions;
using Hanbit.Domain.Content;
using Hanbit.Domain.Hangul;
using Hanbit.Web.Models.Hangul;

using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers.Api;

/// <summary>API stateless cho Hangul (ARCHITECTURE: server tính toán – trình duyệt hiển thị).</summary>
[ApiController]
[Route("api/hangul")]
public sealed class HangulApiController(IContentRepository contentRepository) : ControllerBase
{
    /// <summary>
    /// Ghép âm: <c>GET /api/hangul/compose?c=ㄱ&amp;v=ㅏ&amp;f=ㄴ</c> (f tùy chọn) → <c>{ syllable: "간", romanization: "gan", … }</c>.
    /// Chữ cái không hợp lệ → 400 ValidationProblem (thông báo tiếng Việt theo tham số).
    /// </summary>
    [HttpGet("compose")]
    [ProducesResponseType<ComposeResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public ActionResult<ComposeResponse> Compose([FromQuery] string? c, [FromQuery] string? v, [FromQuery] string? f)
    {
        var (result, error) = HangulComposer.TryCompose(c, v, f);
        if (error is not null)
        {
            ModelState.AddModelError(error.Field, error.Message);
            return ValidationProblem(ModelState);
        }

        return new ComposeResponse(
            result!.Syllable.ToString(),
            result.Romanization,
            result.Initial.ToString(),
            result.Vowel.ToString(),
            result.Final?.ToString());
    }

    /// <summary>
    /// Đề quiz 10 câu cho một nhóm: <c>GET /api/hangul/quiz/{group}?seed=</c>. Câu hỏi KHÔNG chứa đáp án — chấm bằng
    /// <c>POST /api/hangul/quiz/check</c>. Nhóm không tồn tại → 404.
    /// </summary>
    [HttpGet("quiz/{group}")]
    [ProducesResponseType<QuizResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<QuizResponse>> Quiz(string group, [FromQuery] int? seed, CancellationToken cancellationToken)
    {
        var letters = await GroupLettersAsync(group, cancellationToken);
        if (letters is null)
        {
            return NotFound();
        }

        var questions = HangulQuiz.Generate(letters, seed ?? Random.Shared.Next());
        return new QuizResponse(group, questions
            .Select((q, i) => new QuizQuestionDto(i + 1, KindName(q.Kind), q.Prompt, q.Choices))
            .ToList());
    }

    /// <summary>Chấm một câu quiz (stateless): <c>{ group, kind, prompt, answer }</c> → đúng/sai + đáp án đúng.</summary>
    [HttpPost("quiz/check")]
    [ProducesResponseType<QuizCheckResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<QuizCheckResponse>> CheckQuiz(QuizCheckRequest request, CancellationToken cancellationToken)
    {
        var letters = await GroupLettersAsync(request.Group, cancellationToken);
        if (letters is null)
        {
            ModelState.AddModelError(nameof(request.Group), $"Không có nhóm chữ '{request.Group}'.");
            return ValidationProblem(ModelState);
        }

        if (!TryParseKind(request.Kind, out var kind))
        {
            ModelState.AddModelError(nameof(request.Kind), $"Dạng câu hỏi '{request.Kind}' không hợp lệ.");
            return ValidationProblem(ModelState);
        }

        var check = HangulQuiz.Check(letters, kind, request.Prompt ?? string.Empty, request.Answer ?? string.Empty);
        if (check is null)
        {
            ModelState.AddModelError(nameof(request.Prompt), "Câu hỏi không thuộc nhóm chữ này.");
            return ValidationProblem(ModelState);
        }

        return new QuizCheckResponse(check.Correct, check.CorrectAnswer, check.LetterChar, check.Romanization);
    }

    private async Task<IReadOnlyList<HangulLetter>?> GroupLettersAsync(string? group, CancellationToken cancellationToken)
    {
        if (HangulGroupInfo.FindBySlug(group) is not { } info)
        {
            return null;
        }

        var letters = await contentRepository.GetHangulAsync(cancellationToken);
        return letters.Where(l => l.Type == info.Type).ToList();
    }

    private static string KindName(HangulQuizKind kind) => JsonNamingPolicy.KebabCaseLower.ConvertName(kind.ToString());

    private static bool TryParseKind(string? value, out HangulQuizKind kind)
    {
        foreach (var candidate in Enum.GetValues<HangulQuizKind>())
        {
            if (KindName(candidate) == value)
            {
                kind = candidate;
                return true;
            }
        }

        kind = default;
        return false;
    }
}

public sealed record ComposeResponse(string Syllable, string Romanization, string Initial, string Vowel, string? Final);

/// <param name="Kind"><c>char-to-romanization</c> | <c>romanization-to-char</c> | <c>listen-to-char</c>.</param>
/// <param name="Prompt">Chữ cái / phiên âm / chữ được đọc (dạng nghe).</param>
public sealed record QuizQuestionDto(int Number, string Kind, string Prompt, IReadOnlyList<string> Choices);

public sealed record QuizResponse(string Group, IReadOnlyList<QuizQuestionDto> Questions);

public sealed record QuizCheckRequest(string? Group, string? Kind, string? Prompt, string? Answer);

public sealed record QuizCheckResponse(bool Correct, string CorrectAnswer, string Letter, string Romanization);
