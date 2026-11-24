using System.Text.Json;

using Hanbit.Application.Progress;
using Hanbit.Domain.Progress;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hanbit.Web.Controllers.Api;

/// <summary>Kết quả kiểm tra file tiến độ: bản sạch để ghi vào localStorage + các điều chỉnh đã làm.</summary>
public sealed record ProgressValidateResponse(LearnerProgress Progress, IReadOnlyList<string> Warnings);

[ApiController]
public sealed class ProgressValidateController(ProgressImporter importer, IOptions<Microsoft.AspNetCore.Mvc.JsonOptions> mvcJson) : ControllerBase
{
    /// <summary>Giới hạn kích thước file sao lưu (≈ 20.000 thẻ).</summary>
    public const long MaxBytes = 5 * 1024 * 1024;

    /// <summary>
    /// <c>POST /api/progress/validate</c> — thân là nội dung file sao lưu (JSON). Kiểm tra + migrate + làm sạch → trả bản
    /// sạch. Sai định dạng / phiên bản mới hơn → 400 với thông báo tiếng Việt.
    /// </summary>
    [HttpPost("api/progress/validate")]
    [RequestSizeLimit(MaxBytes)]
    [ProducesResponseType<ProgressValidateResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public ActionResult<ProgressValidateResponse> Validate([FromBody] JsonElement body)
    {
        var result = importer.Import(body, mvcJson.Value.JsonSerializerOptions);
        if (result.Progress is null)
        {
            ModelState.AddModelError("file", result.Error ?? "Tệp không hợp lệ.");
            return ValidationProblem(ModelState);
        }

        return new ProgressValidateResponse(result.Progress, result.Warnings);
    }
}
