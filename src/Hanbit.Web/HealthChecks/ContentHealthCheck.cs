using Hanbit.Application.Abstractions;

using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hanbit.Web.HealthChecks;

/// <summary>/health chỉ Healthy khi đọc được nội dung học (bắt lỗi thiếu/sai đường dẫn content/ trong container).</summary>
public sealed class ContentHealthCheck(IContentRepository content, ILogger<ContentHealthCheck> logger) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var letters = await content.GetHangulAsync(cancellationToken);
            return letters.Count > 0
                ? HealthCheckResult.Healthy()
                : HealthCheckResult.Unhealthy("hangul.json không có chữ cái nào.");
        }
        catch (Exception ex) when (ex is IOException or System.Text.Json.JsonException or UnauthorizedAccessException)
        {
            logger.LogError(ex, "Không đọc được nội dung học.");
            return HealthCheckResult.Unhealthy("Không đọc được nội dung học.", ex);
        }
    }
}
