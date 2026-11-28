using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

using Hanbit.Domain.Reminders;
using Hanbit.Infrastructure.Reminders;

using Microsoft.Extensions.Logging.Abstractions;

using Shouldly;

namespace Hanbit.Infrastructure.Tests.Reminders;

/// <summary>Giai đoạn 9: mã hóa Web Push (RFC 8291) + VAPID (RFC 8292).</summary>
public class WebPushCryptoTests
{
    private static byte[] B64(string value) => WebPushCrypto.Base64UrlDecode(value);

    /// <summary>RFC 8291 Phụ lục A — ví dụ mã hóa chính thức.</summary>
    [Fact]
    public void Encrypt_MatchesRfc8291AppendixA()
    {
        var body = WebPushCrypto.Encrypt(
            Encoding.UTF8.GetBytes("When I grow up, I want to be a watermelon"),
            uaPublicKey: B64("BCVxsr7N_eNgVRqvHtD0zTZsEc6-VV-JvLexhqUzORcxaOzi6-AYWXvTBHm4bjyPjs7Vd8pZGH6SRpkNtoIAiw4"),
            authSecret: B64("BTBZMqHH6r4Tts7J_aSIgg"),
            senderPrivateKey: B64("yfWPiYE-n46HLnH0KqZOF1fJJU3MYrct3AELtAQ-oRw"),
            salt: B64("DGv6ra1nlYgDCS1FRnbzlw"));

        WebPushCrypto.Base64UrlEncode(body).ShouldBe(
            "DGv6ra1nlYgDCS1FRnbzlwAAEABBBP4z9KsN6nGRTbVYI_c7VJSPQTBtkgcy27mlmlMoZIIgDll6e3vCYLocInmYWAmS6TlzAC8wEqKK6PBru3jl7A_yl95bQpu6cVPTpK4Mqgkf1CXztLVBSt2Ks3oZwbuwXPXLWyouBWLVWGNWQexSgSxsj_Qulcy4a-fN");
    }

    [Fact]
    public void Encrypt_RandomKeys_DecryptableByReceiver()
    {
        using var ua = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var q = ua.ExportParameters(false).Q;
        byte[] uaPublic = [0x04, .. q.X!, .. q.Y!];
        var auth = RandomNumberGenerator.GetBytes(16);
        var message = Encoding.UTF8.GetBytes("""{"title":"Hanbit","body":"Đến giờ học rồi!"}""");

        var body = WebPushCrypto.Encrypt(message, uaPublic, auth);

        Decrypt(body, ua, uaPublic, auth).ShouldBe(message);
    }

    [Fact]
    public void Encrypt_RejectsBadKeysAndOversizedPayload()
    {
        Should.Throw<ArgumentException>(() => WebPushCrypto.Encrypt([1], new byte[64], new byte[16]));
        Should.Throw<ArgumentException>(() => WebPushCrypto.Encrypt([1], [0x04, .. new byte[64]], new byte[15]));
        Should.Throw<ArgumentException>(() => WebPushCrypto.Encrypt(new byte[4000], [0x04, .. new byte[64]], new byte[16]));
    }

    [Fact]
    public void VapidAuthorization_SignedJwt_VerifiesWithPublicKey()
    {
        using var vapid = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = vapid.ExportParameters(true);
        var publicKey = WebPushCrypto.Base64UrlEncode([0x04, .. p.Q.X!, .. p.Q.Y!]);
        var privateKey = WebPushCrypto.Base64UrlEncode(p.D);
        var now = new DateTimeOffset(2026, 11, 28, 12, 0, 0, TimeSpan.Zero);

        var header = WebPushCrypto.VapidAuthorization(
            new Uri("https://fcm.googleapis.com/fcm/send/abc"), "https://hanbit.onrender.com", publicKey, privateKey, now, TimeSpan.FromHours(12));

        header.ShouldStartWith("vapid t=");
        header.ShouldEndWith($", k={publicKey}");
        var jwt = header["vapid t=".Length..header.IndexOf(',', StringComparison.Ordinal)].Split('.');
        jwt.Length.ShouldBe(3);
        var claims = JsonDocument.Parse(B64(jwt[1])).RootElement;
        claims.GetProperty("aud").GetString().ShouldBe("https://fcm.googleapis.com");
        claims.GetProperty("sub").GetString().ShouldBe("https://hanbit.onrender.com");
        claims.GetProperty("exp").GetInt64().ShouldBe(now.AddHours(12).ToUnixTimeSeconds());
        JsonDocument.Parse(B64(jwt[0])).RootElement.GetProperty("alg").GetString().ShouldBe("ES256");
        vapid.VerifyData(Encoding.ASCII.GetBytes($"{jwt[0]}.{jwt[1]}"), B64(jwt[2]), HashAlgorithmName.SHA256).ShouldBeTrue();
    }

    /// <summary>Giải mã phía trình duyệt (RFC 8291 §3.4 + RFC 8188), viết độc lập để kiểm tra.</summary>
    internal static byte[] Decrypt(byte[] body, ECDiffieHellman ua, byte[] uaPublic, byte[] auth)
    {
        var salt = body[..16];
        var asPublic = body[21..86];
        using var sender = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = asPublic[1..33], Y = asPublic[33..] },
        });
        var secret = ua.DeriveRawSecretAgreement(sender.PublicKey);
        byte[] info = [.. Encoding.ASCII.GetBytes("WebPush: info\0"), .. uaPublic, .. asPublic];
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, secret, 32, auth, info);
        var cek = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0"));
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, Encoding.ASCII.GetBytes("Content-Encoding: nonce\0"));

        var ciphertext = body[86..^16];
        var plain = new byte[ciphertext.Length];
        using var aes = new AesGcm(cek, 16);
        aes.Decrypt(nonce, ciphertext, body[^16..], plain);
        plain[^1].ShouldBe((byte)0x02); // dấu record cuối
        return plain[..^1];
    }
}

/// <summary>Gửi tới dịch vụ push giả: header chuẩn, ánh xạ lỗi.</summary>
public class WebPushSenderTests
{
    private sealed class FakePushService(HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? Request { get; private set; }

        public byte[]? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = request;
            Body = request.Content is null ? null : await request.Content.ReadAsByteArrayAsync(cancellationToken);
            return new HttpResponseMessage(status);
        }
    }

    private sealed class FixedTime(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static readonly PushMessage Message = new("Hanbit", "Đến giờ học rồi!", "/dashboard", "hanbit-reminder");

    private static (PushDevice Device, ECDiffieHellman Key, byte[] Public, byte[] Auth) Device()
    {
        var key = ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256);
        var q = key.ExportParameters(false).Q;
        byte[] uaPublic = [0x04, .. q.X!, .. q.Y!];
        var auth = RandomNumberGenerator.GetBytes(16);
        var device = new PushDevice(
            "https://fcm.googleapis.com/fcm/send/abc123", WebPushCrypto.Base64UrlEncode(uaPublic), WebPushCrypto.Base64UrlEncode(auth));
        return (device, key, uaPublic, auth);
    }

    private static (WebPushSender Sender, FakePushService Service) Create(HttpStatusCode status)
    {
        using var vapid = ECDsa.Create(ECCurve.NamedCurves.nistP256);
        var p = vapid.ExportParameters(true);
        var options = new PushOptions
        {
            PublicKey = WebPushCrypto.Base64UrlEncode([0x04, .. p.Q.X!, .. p.Q.Y!]),
            PrivateKey = WebPushCrypto.Base64UrlEncode(p.D),
        };
        var service = new FakePushService(status);
        var clock = new FixedTime(new DateTimeOffset(2026, 11, 28, 13, 0, 0, TimeSpan.Zero));
        return (new WebPushSender(new HttpClient(service), options, clock, NullLogger<WebPushSender>.Instance), service);
    }

    [Fact]
    public async Task Send_PostsEncryptedPayload_WithVapidTtlAndEncoding()
    {
        var (sender, service) = Create(HttpStatusCode.Created);
        var (device, key, uaPublic, auth) = Device();
        using (key)
        {
            (await sender.SendAsync(device, Message, default)).ShouldBe(PushSendStatus.Sent);

            var request = service.Request.ShouldNotBeNull();
            request.Method.ShouldBe(HttpMethod.Post);
            request.RequestUri!.ToString().ShouldBe(device.Endpoint);
            request.Headers.GetValues("TTL").ShouldBe([WebPushSender.TimeToLiveSeconds.ToString(System.Globalization.CultureInfo.InvariantCulture)]);
            request.Headers.GetValues("Authorization").Single().ShouldStartWith("vapid t=");
            request.Content!.Headers.ContentEncoding.ShouldBe(["aes128gcm"]);

            var payload = JsonDocument.Parse(WebPushCryptoTests.Decrypt(service.Body!, key, uaPublic, auth)).RootElement;
            payload.GetProperty("title").GetString().ShouldBe("Hanbit");
            payload.GetProperty("body").GetString().ShouldBe("Đến giờ học rồi!");
            payload.GetProperty("url").GetString().ShouldBe("/dashboard");
            payload.GetProperty("tag").GetString().ShouldBe("hanbit-reminder");
        }
    }

    [Theory]
    [InlineData(HttpStatusCode.Gone, PushSendStatus.Gone)]
    [InlineData(HttpStatusCode.NotFound, PushSendStatus.Gone)]
    [InlineData(HttpStatusCode.InternalServerError, PushSendStatus.Failed)]
    [InlineData(HttpStatusCode.TooManyRequests, PushSendStatus.Failed)]
    public async Task Send_MapsPushServiceResponses(HttpStatusCode status, PushSendStatus expected)
    {
        var (sender, _) = Create(status);
        var (device, key, _, _) = Device();
        using (key)
        {
            (await sender.SendAsync(device, Message, default)).ShouldBe(expected);
        }
    }

    [Fact]
    public async Task Send_BrokenDeviceKey_GoneWithoutCallingService()
    {
        var (sender, service) = Create(HttpStatusCode.Created);
        var (device, key, _, _) = Device();
        using (key)
        {
            (await sender.SendAsync(device with { P256dh = "khong-hop-le" }, Message, default)).ShouldBe(PushSendStatus.Gone);
            service.Request.ShouldBeNull();
        }
    }
}
