using System.Security.Claims;
using System.Text.Json;

using Hanbit.Application.Progress;
using Hanbit.Domain.Progress;
using Hanbit.Web.Hosting;

using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Hanbit.Web.Controllers.Api;

/// <param name="Progress">Tiến độ trên thiết bị (như localStorage); null → chỉ lấy bản trên tài khoản.</param>
/// <param name="BaseXp">XP của thiết bị ngay sau lần đồng bộ trước; dữ liệu khách chưa đồng bộ → 0.</param>
/// <param name="Mode"><c>merge</c> (mặc định) hoặc <c>replace</c> (nhập file sao lưu / xóa tiến độ).</param>
public sealed record ProgressSyncRequest(JsonElement? Progress, int BaseXp = 0, ProgressSyncMode Mode = ProgressSyncMode.Merge);

/// <param name="Progress">Bản trên tài khoản sau khi gộp (null = tài khoản chưa có tiến độ).</param>
public sealed record ProgressSyncResponse(LearnerProgress? Progress);

/// <summary>
/// Đồng bộ tiến độ với tài khoản (T8.3). Chỉ khi đã đăng nhập; tắt tài khoản → 404, chưa đăng nhập → 401.
/// Request ghi cần header <c>X-CSRF-TOKEN</c> (<see cref="AuthenticatedApiAntiforgeryFilter"/>).
/// </summary>
[ApiController]
[Route("api/progress/sync")]
public sealed class ProgressSyncController(
    AccountFeature accounts,
    IServiceProvider services,
    IOptions<JsonOptions> mvcJson) : ControllerBase
{
    [HttpGet]
    [ProducesResponseType<ProgressSyncResponse>(StatusCodes.Status200OK)]
    public async Task<ActionResult<ProgressSyncResponse>> Get(CancellationToken cancellationToken)
    {
        if (Guard() is { } blocked)
        {
            return blocked;
        }

        return new ProgressSyncResponse(await Sync.GetAsync(UserId, cancellationToken));
    }

    [HttpPut]
    [RequestSizeLimit(ProgressValidateController.MaxBytes)]
    [ProducesResponseType<ProgressSyncResponse>(StatusCodes.Status200OK)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<ProgressSyncResponse>> Put([FromBody] ProgressSyncRequest request, CancellationToken cancellationToken)
    {
        if (Guard() is { } blocked)
        {
            return blocked;
        }

        var result = await Sync.SyncAsync(UserId, request.Progress, request.BaseXp, request.Mode, mvcJson.Value.JsonSerializerOptions, cancellationToken);
        if (result.Error is not null)
        {
            ModelState.AddModelError(nameof(request.Progress), result.Error);
            return ValidationProblem(ModelState);
        }

        return new ProgressSyncResponse(result.Progress);
    }

    // Dịch vụ chỉ đăng ký khi có DB → lấy sau khi đã kiểm tra tài khoản bật.
    private ProgressSyncService Sync => services.GetRequiredService<ProgressSyncService>();

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private ActionResult? Guard()
    {
        if (!accounts.Enabled)
        {
            return NotFound();
        }

        return User.Identity?.IsAuthenticated == true && User.FindFirstValue(ClaimTypes.NameIdentifier) is not null ? null : Unauthorized();
    }
}
