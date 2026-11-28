using System.Globalization;
using System.Security.Claims;

using Hanbit.Application.Reminders;
using Hanbit.Domain.Reminders;
using Hanbit.Web.Hosting;

using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers.Api;

/// <param name="Enabled">Người học đã bật nhắc chưa.</param>
/// <param name="Time">Giờ nhắc <c>HH:mm</c> (giờ Việt Nam).</param>
/// <param name="Devices">Số thiết bị đang nhận thông báo.</param>
/// <param name="PublicKey">Khóa VAPID công khai — <c>pushManager.subscribe({ applicationServerKey })</c>.</param>
public sealed record ReminderSettingsResponse(bool Enabled, string Time, int Devices, IReadOnlyList<string> AllowedTimes, string PublicKey);

public sealed record ReminderSettingsRequest(bool Enabled, string Time);

public sealed record PushKeys(string P256dh, string Auth);

/// <summary>Đúng cấu trúc <c>PushSubscription.toJSON()</c> của trình duyệt.</summary>
public sealed record PushSubscriptionRequest(string Endpoint, PushKeys Keys);

public sealed record PushEndpointRequest(string Endpoint);

/// <summary>
/// Nhắc học (giai đoạn 9 — T9.1): cài đặt, đăng ký thiết bị, gửi thử. Tắt (thiếu DB/Google/VAPID) → 404; chưa đăng nhập → 401;
/// request ghi cần <c>X-CSRF-TOKEN</c>.
/// </summary>
[ApiController]
[Route("api/reminders")]
public sealed class RemindersApiController(ReminderFeature feature, IServiceProvider services) : ControllerBase
{
    public const string TimeFormat = "HH:mm";

    [HttpGet]
    public async Task<ActionResult<ReminderSettingsResponse>> Get(CancellationToken cancellationToken)
    {
        if (Guard() is { } blocked)
        {
            return blocked;
        }

        var settings = await Reminders.GetSettingsAsync(UserId, cancellationToken);
        var devices = await Reminders.GetDevicesAsync(UserId, cancellationToken);
        return new ReminderSettingsResponse(
            settings.Enabled,
            Format(settings.Time),
            devices.Count,
            [.. ReminderSettings.AllowedTimes.Select(Format)],
            feature.PublicKey!);
    }

    [HttpPut]
    public async Task<IActionResult> Put([FromBody] ReminderSettingsRequest request, CancellationToken cancellationToken)
    {
        if (Guard() is { } blocked)
        {
            return blocked;
        }

        var error = TimeOnly.TryParseExact(request.Time, TimeFormat, CultureInfo.InvariantCulture, DateTimeStyles.None, out var time)
            ? await Reminders.UpdateSettingsAsync(UserId, request.Enabled, time, cancellationToken)
            : "Giờ nhắc không hợp lệ (cần dạng HH:mm).";
        return error is null ? NoContent() : Problem(error);
    }

    [HttpPost("subscriptions")]
    public async Task<IActionResult> Subscribe([FromBody] PushSubscriptionRequest request, CancellationToken cancellationToken)
    {
        if (Guard() is { } blocked)
        {
            return blocked;
        }

        var error = await Reminders.SubscribeAsync(UserId, new PushDevice(request.Endpoint, request.Keys?.P256dh ?? "", request.Keys?.Auth ?? ""), cancellationToken);
        return error is null ? NoContent() : Problem(error);
    }

    [HttpPost("subscriptions/remove")]
    public async Task<IActionResult> Unsubscribe([FromBody] PushEndpointRequest request, CancellationToken cancellationToken)
    {
        if (Guard() is { } blocked)
        {
            return blocked;
        }

        await Reminders.UnsubscribeAsync(UserId, request.Endpoint ?? "", cancellationToken);
        return NoContent();
    }

    [HttpPost("test")]
    public async Task<ActionResult<PushTestResult>> Test(CancellationToken cancellationToken)
    {
        if (Guard() is { } blocked)
        {
            return blocked;
        }

        return await Reminders.SendTestAsync(UserId, cancellationToken);
    }

    private static string Format(TimeOnly time) => time.ToString(TimeFormat, CultureInfo.InvariantCulture);

    private ObjectResult Problem(string error)
    {
        ModelState.AddModelError("reminder", error);
        return (ObjectResult)ValidationProblem(ModelState);
    }

    // Dịch vụ chỉ đăng ký khi bật nhắc học → lấy sau khi đã kiểm tra.
    private ReminderService Reminders => services.GetRequiredService<ReminderService>();

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    private ActionResult? Guard()
    {
        if (!feature.Enabled)
        {
            return NotFound();
        }

        return User.Identity?.IsAuthenticated == true && User.FindFirstValue(ClaimTypes.NameIdentifier) is not null ? null : Unauthorized();
    }
}
