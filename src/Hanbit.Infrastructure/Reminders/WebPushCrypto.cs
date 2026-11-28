using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Hanbit.Infrastructure.Reminders;

/// <summary>
/// Web Push theo chuẩn hiện hành, chỉ dùng crypto có sẵn của .NET:
/// <list type="bullet">
/// <item>RFC 8291 — mã hóa nội dung <c>aes128gcm</c> (ECDH P-256 + HKDF-SHA-256 + AES-128-GCM, RFC 8188).</item>
/// <item>RFC 8292 — VAPID: JWT ES256, header <c>Authorization: vapid t=…, k=…</c>.</item>
/// </list>
/// (Package NuGet <c>WebPush</c> còn dùng bản nháp <c>aesgcm</c> — dịch vụ push của Apple không nhận.)
/// </summary>
public static class WebPushCrypto
{
    /// <summary>Kích thước record (RFC 8188) — nội dung nhắc học luôn nằm gọn trong 1 record.</summary>
    public const int RecordSize = 4096;

    /// <summary>Tối đa của dịch vụ push cho phần nội dung đã mã hóa.</summary>
    public const int MaxPlaintextBytes = 3993;

    private static readonly byte[] KeyInfoPrefix = Encoding.ASCII.GetBytes("WebPush: info\0");
    private static readonly byte[] CekInfo = Encoding.ASCII.GetBytes("Content-Encoding: aes128gcm\0");
    private static readonly byte[] NonceInfo = Encoding.ASCII.GetBytes("Content-Encoding: nonce\0");

    /// <summary>
    /// Mã hóa <paramref name="plaintext"/> cho một thiết bị. <paramref name="senderPrivateKey"/>/<paramref name="salt"/> chỉ truyền
    /// khi kiểm thử (vector RFC 8291 Phụ lục A); bình thường sinh ngẫu nhiên mỗi lần gửi.
    /// </summary>
    /// <param name="uaPublicKey">Khóa công khai của trình duyệt (<c>p256dh</c>, 65 byte dạng không nén).</param>
    /// <param name="authSecret">Bí mật xác thực của trình duyệt (<c>auth</c>, 16 byte).</param>
    /// <returns>Thân request: header (salt ‖ rs ‖ idlen ‖ keyid) ‖ ciphertext.</returns>
    public static byte[] Encrypt(byte[] plaintext, byte[] uaPublicKey, byte[] authSecret, byte[]? senderPrivateKey = null, byte[]? salt = null)
    {
        if (plaintext.Length > MaxPlaintextBytes)
        {
            throw new ArgumentException("Nội dung thông báo quá dài.", nameof(plaintext));
        }

        if (uaPublicKey.Length != 65 || uaPublicKey[0] != 0x04)
        {
            throw new ArgumentException("Khóa p256dh phải là điểm P-256 không nén (65 byte).", nameof(uaPublicKey));
        }

        if (authSecret.Length != 16)
        {
            throw new ArgumentException("Bí mật auth phải dài 16 byte.", nameof(authSecret));
        }

        salt ??= RandomNumberGenerator.GetBytes(16);

        using var sender = senderPrivateKey is null
            ? ECDiffieHellman.Create(ECCurve.NamedCurves.nistP256)
            : ECDiffieHellman.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, D = senderPrivateKey, Q = PublicPoint(senderPrivateKey) });
        var asPublic = Uncompressed(sender.ExportParameters(false).Q);

        using var receiver = ECDiffieHellman.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            Q = new ECPoint { X = uaPublicKey[1..33], Y = uaPublicKey[33..65] },
        });
        var ecdhSecret = sender.DeriveRawSecretAgreement(receiver.PublicKey);

        // RFC 8291 §3.4: IKM = HKDF(salt = auth_secret, ikm = ecdh_secret, info = "WebPush: info\0" ‖ ua_public ‖ as_public, 32)
        var keyInfo = new byte[KeyInfoPrefix.Length + 65 + 65];
        KeyInfoPrefix.CopyTo(keyInfo, 0);
        uaPublicKey.CopyTo(keyInfo, KeyInfoPrefix.Length);
        asPublic.CopyTo(keyInfo, KeyInfoPrefix.Length + 65);
        var ikm = HKDF.DeriveKey(HashAlgorithmName.SHA256, ecdhSecret, 32, authSecret, keyInfo);

        // RFC 8188 §2.2–2.3
        var cek = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 16, salt, CekInfo);
        var nonce = HKDF.DeriveKey(HashAlgorithmName.SHA256, ikm, 12, salt, NonceInfo);

        // Một record duy nhất: dữ liệu ‖ 0x02 (dấu record cuối)
        var padded = new byte[plaintext.Length + 1];
        plaintext.CopyTo(padded, 0);
        padded[^1] = 0x02;

        var ciphertext = new byte[padded.Length];
        var tag = new byte[16];
        using (var aes = new AesGcm(cek, tag.Length))
        {
            aes.Encrypt(nonce, padded, ciphertext, tag);
        }

        var body = new byte[16 + 4 + 1 + 65 + ciphertext.Length + tag.Length];
        salt.CopyTo(body, 0);
        BinaryPrimitives.WriteUInt32BigEndian(body.AsSpan(16, 4), RecordSize);
        body[20] = 65;
        asPublic.CopyTo(body, 21);
        ciphertext.CopyTo(body, 86);
        tag.CopyTo(body, 86 + ciphertext.Length);
        return body;
    }

    /// <summary>Header <c>Authorization</c> VAPID (RFC 8292) cho endpoint; JWT hết hạn sau <paramref name="lifetime"/> (tối đa 24 giờ).</summary>
    /// <param name="publicKey">Khóa công khai VAPID, base64url 65 byte.</param>
    /// <param name="privateKey">Khóa bí mật VAPID, base64url 32 byte.</param>
    public static string VapidAuthorization(Uri endpoint, string subject, string publicKey, string privateKey, DateTimeOffset now, TimeSpan lifetime)
    {
        var audience = endpoint.GetLeftPart(UriPartial.Authority);
        var header = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, string> { ["typ"] = "JWT", ["alg"] = "ES256" }));
        var claims = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(new Dictionary<string, object>
        {
            ["aud"] = audience,
            ["exp"] = now.Add(lifetime).ToUnixTimeSeconds(),
            ["sub"] = subject,
        }));
        var signingInput = Encoding.ASCII.GetBytes($"{header}.{claims}");

        var d = Base64UrlDecode(privateKey);
        var q = Base64UrlDecode(publicKey);
        using var ecdsa = ECDsa.Create(new ECParameters
        {
            Curve = ECCurve.NamedCurves.nistP256,
            D = d,
            Q = new ECPoint { X = q[1..33], Y = q[33..65] },
        });
        var signature = ecdsa.SignData(signingInput, HashAlgorithmName.SHA256, DSASignatureFormat.IeeeP1363FixedFieldConcatenation);

        return $"vapid t={header}.{claims}.{Base64UrlEncode(signature)}, k={publicKey}";
    }

    public static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static byte[] Base64UrlDecode(string value)
    {
        var base64 = value.Replace('-', '+').Replace('_', '/');
        return Convert.FromBase64String(base64.PadRight(base64.Length + ((4 - (base64.Length % 4)) % 4), '='));
    }

    private static byte[] Uncompressed(ECPoint point)
    {
        var bytes = new byte[65];
        bytes[0] = 0x04;
        point.X!.CopyTo(bytes, 1);
        point.Y!.CopyTo(bytes, 33);
        return bytes;
    }

    /// <summary>Tính khóa công khai từ khóa bí mật (chỉ dùng cho vector kiểm thử).</summary>
    private static ECPoint PublicPoint(byte[] privateKey)
    {
        using var key = ECDiffieHellman.Create(new ECParameters { Curve = ECCurve.NamedCurves.nistP256, D = privateKey });
        return key.ExportParameters(false).Q;
    }
}
