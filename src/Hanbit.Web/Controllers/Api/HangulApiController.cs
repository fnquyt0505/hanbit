using Hanbit.Domain.Hangul;

using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers.Api;

/// <summary>API stateless cho Hangul (ARCHITECTURE: server tính toán – trình duyệt hiển thị).</summary>
[ApiController]
[Route("api/hangul")]
public sealed class HangulApiController : ControllerBase
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
}

public sealed record ComposeResponse(string Syllable, string Romanization, string Initial, string Vowel, string? Final);
