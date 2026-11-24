using System.Globalization;
using System.Threading.RateLimiting;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Hanbit.Web.Hosting;

/// <summary>Cấu hình giới hạn tần suất (appsettings mục <c>RateLimit</c>).</summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimit";

    /// <summary>Số request /api/* tối đa mỗi IP mỗi phút.</summary>
    public int ApiPermitPerMinute { get; set; } = 120;
}

/// <summary>
/// T7.3: rate limiting cho <c>/api/*</c>, security headers (CSP…), Cache-Control.
/// </summary>
public static class HttpHardening
{
    /// <summary>
    /// CSP: chỉ tài nguyên cùng nguồn + font CDN (Google Fonts, jsDelivr — chốt 2026-11-23).
    /// <c>'unsafe-eval'</c>: Alpine.js bản chuẩn biên dịch biểu thức trong template bằng Function.
    /// <c>'unsafe-inline'</c> chỉ cho style (thuộc tính style= của thanh tiến độ, x-bind:style).
    /// </summary>
    public static string ContentSecurityPolicy { get; } = string.Join("; ",
        "default-src 'self'",
        $"script-src 'self' 'unsafe-eval' {InlineScripts.ThemeInitHash}",
        "style-src 'self' 'unsafe-inline' https://fonts.googleapis.com https://cdn.jsdelivr.net",
        "font-src 'self' https://fonts.gstatic.com https://cdn.jsdelivr.net",
        "img-src 'self' data:",
        "media-src 'self' blob:",
        "connect-src 'self'",
        "object-src 'none'",
        "base-uri 'self'",
        "form-action 'self'",
        "frame-ancestors 'none'");

    public const string ImmutableCache = "public, max-age=31536000, immutable";

    public static IServiceCollection AddHttpHardening(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();

        services.AddRateLimiter(limiter =>
        {
            limiter.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            limiter.GlobalLimiter = PartitionedRateLimiter.Create<HttpContext, string>(context =>
                context.Request.Path.StartsWithSegments("/api")
                    ? RateLimitPartition.GetFixedWindowLimiter(
                        $"api:{context.Connection.RemoteIpAddress}",
                        _ => new FixedWindowRateLimiterOptions
                        {
                            PermitLimit = Math.Max(1, options.ApiPermitPerMinute),
                            Window = TimeSpan.FromMinutes(1),
                            QueueLimit = 0,
                        })
                    : RateLimitPartition.GetNoLimiter("site"));
            limiter.OnRejected = async (context, cancellationToken) =>
            {
                var retryAfter = context.Lease.TryGetMetadata(MetadataName.RetryAfter, out var after) ? after : TimeSpan.FromMinutes(1);
                context.HttpContext.Response.Headers.RetryAfter = ((int)Math.Ceiling(retryAfter.TotalSeconds)).ToString(CultureInfo.InvariantCulture);
                await context.HttpContext.Response.WriteAsJsonAsync(
                    new ProblemDetails
                    {
                        Status = StatusCodes.Status429TooManyRequests,
                        Title = "Quá nhiều yêu cầu",
                        Detail = "Bạn thao tác quá nhanh — chờ một chút rồi thử lại nhé.",
                    },
                    options: null,
                    contentType: "application/problem+json",
                    cancellationToken: cancellationToken);
            };
        });

        return services;
    }

    /// <summary>Security headers + Cache-Control cho mọi phản hồi (đặt trước static assets và endpoints).</summary>
    public static IApplicationBuilder UseSecurityHeaders(this IApplicationBuilder app) => app.Use(async (context, next) =>
    {
        context.Response.OnStarting(() =>
        {
            var headers = context.Response.Headers;
            var request = context.Request;
            headers.XContentTypeOptions = "nosniff";
            headers["Referrer-Policy"] = "strict-origin-when-cross-origin";
            headers["Permissions-Policy"] = "camera=(), microphone=(), geolocation=(), payment=(), usb=()";
            headers["Cross-Origin-Opener-Policy"] = "same-origin";
            headers.XFrameOptions = "DENY";

            var contentType = context.Response.ContentType ?? string.Empty;
            if (contentType.StartsWith("text/html", StringComparison.OrdinalIgnoreCase))
            {
                headers.ContentSecurityPolicy = ContentSecurityPolicy;
                headers.CacheControl = "no-cache";
            }
            else if (request.Path.StartsWithSegments("/api"))
            {
                headers.CacheControl = "no-store";
            }
            else if (request.Query.ContainsKey("v") && context.Response.StatusCode == StatusCodes.Status200OK)
            {
                // asp-append-version: ?v= là mã băm nội dung → đổi file là đổi URL → cache vĩnh viễn an toàn.
                headers.CacheControl = ImmutableCache;
            }

            return Task.CompletedTask;
        });

        await next();
    });
}
