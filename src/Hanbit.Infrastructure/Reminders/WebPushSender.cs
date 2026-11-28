using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text.Json;

using Hanbit.Application.Abstractions;
using Hanbit.Domain.Reminders;

using Microsoft.Extensions.Logging;

namespace Hanbit.Infrastructure.Reminders;

/// <summary>
/// Khóa VAPID (giai đoạn 9): cặp khóa P-256 dạng base64url. Render: <c>Push__PublicKey</c>, <c>Push__PrivateKey</c> (<c>sync: false</c>),
/// tạo trên máy người dùng bằng <c>npx web-push generate-vapid-keys</c>. <see cref="Subject"/> = URL site hoặc <c>mailto:</c>.
/// </summary>
public sealed class PushOptions
{
    public const string SectionName = "Push";

    public string? PublicKey { get; set; }

    public string? PrivateKey { get; set; }

    public string Subject { get; set; } = "https://hanbit.onrender.com";

    public bool IsConfigured => !string.IsNullOrWhiteSpace(PublicKey) && !string.IsNullOrWhiteSpace(PrivateKey);
}

/// <summary><see cref="IPushSender"/>: mã hóa <c>aes128gcm</c> + VAPID (<see cref="WebPushCrypto"/>), POST tới dịch vụ push.</summary>
public sealed class WebPushSender(HttpClient httpClient, PushOptions options, TimeProvider time, ILogger<WebPushSender> logger) : IPushSender
{
    /// <summary>Dịch vụ push giữ thông báo tối đa 12 giờ nếu thiết bị đang tắt (nhắc học quá hạn thì vô nghĩa).</summary>
    public const int TimeToLiveSeconds = 12 * 60 * 60;

    private static readonly TimeSpan JwtLifetime = TimeSpan.FromHours(12);
    private static readonly JsonSerializerOptions PayloadJson = new(JsonSerializerDefaults.Web);

    public async Task<PushSendStatus> SendAsync(PushDevice device, PushMessage message, CancellationToken cancellationToken)
    {
        byte[] body;
        Uri endpoint;
        try
        {
            endpoint = new Uri(device.Endpoint);
            var payload = JsonSerializer.SerializeToUtf8Bytes(new { message.Title, message.Body, message.Url, message.Tag }, PayloadJson);
            body = WebPushCrypto.Encrypt(payload, WebPushCrypto.Base64UrlDecode(device.P256dh), WebPushCrypto.Base64UrlDecode(device.Auth));
        }
        catch (Exception ex) when (ex is FormatException or ArgumentException or CryptographicException or UriFormatException)
        {
            // Khóa của thiết bị hỏng → không bao giờ gửi được, xóa như đăng ký hết hạn.
            return PushSendStatus.Gone;
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, endpoint) { Content = new ByteArrayContent(body) };
        request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream");
        request.Content.Headers.ContentEncoding.Add("aes128gcm");
        request.Headers.Add("TTL", TimeToLiveSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture));
        request.Headers.Add("Urgency", "normal");
        request.Headers.TryAddWithoutValidation(
            "Authorization",
            WebPushCrypto.VapidAuthorization(endpoint, options.Subject, options.PublicKey!, options.PrivateKey!, time.GetUtcNow(), JwtLifetime));

        try
        {
            using var response = await httpClient.SendAsync(request, cancellationToken);
            if (response.IsSuccessStatusCode)
            {
                return PushSendStatus.Sent;
            }

            if (response.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Gone)
            {
                return PushSendStatus.Gone;
            }

            // Không ghi endpoint vào log (định danh thiết bị).
            logger.LogWarning("Dịch vụ push {Host} trả {Status}.", endpoint.Host, (int)response.StatusCode);
            return PushSendStatus.Failed;
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Gửi Web Push tới {Host} thất bại.", endpoint.Host);
            return PushSendStatus.Failed;
        }
    }
}
