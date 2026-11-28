using System.Security.Cryptography;
using System.Text;

using Hanbit.Application.Reminders;
using Hanbit.Web.Hosting;

using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers;

/// <summary>
/// Chạy gửi nhắc học (giai đoạn 9 — T9.2). Gọi bởi GitHub Actions cron (<c>.github/workflows/reminders.yml</c>) với
/// <c>Authorization: Bearer &lt;Reminders:RunToken&gt;</c>. Chưa bật nhắc học hoặc chưa đặt token → 404; sai token → 401.
/// Không nằm dưới <c>/api</c>: không rate limit theo IP, không cần CSRF (không dùng cookie).
/// </summary>
[ApiController]
[Route("internal/reminders")]
public sealed class InternalRemindersController(ReminderFeature feature, ReminderRunOptions options, IServiceProvider services) : ControllerBase
{
    [HttpPost("run")]
    public async Task<ActionResult<ReminderRunResult>> Run(CancellationToken cancellationToken)
    {
        if (!feature.Enabled || !options.IsConfigured)
        {
            return NotFound();
        }

        if (!IsAuthorized(Request.Headers.Authorization.ToString(), options.RunToken!))
        {
            return Unauthorized();
        }

        return await services.GetRequiredService<ReminderJob>().RunAsync(cancellationToken);
    }

    /// <summary>So sánh token thời gian hằng (không lộ độ dài khớp qua thời gian phản hồi).</summary>
    public static bool IsAuthorized(string authorizationHeader, string token)
    {
        const string prefix = "Bearer ";
        if (!authorizationHeader.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var given = SHA256.HashData(Encoding.UTF8.GetBytes(authorizationHeader[prefix.Length..].Trim()));
        var expected = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return CryptographicOperations.FixedTimeEquals(given, expected);
    }
}
