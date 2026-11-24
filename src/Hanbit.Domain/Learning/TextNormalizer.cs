using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Hanbit.Domain.Learning;

/// <summary>Chuẩn hóa câu trả lời trước khi so sánh (bài gõ, sắp xếp, nối cặp).</summary>
public static partial class TextNormalizer
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex WhitespaceRegex();

    /// <summary>
    /// NFC (bàn phím Hàn/Việt có thể gửi dạng tách rời) → gộp mọi khoảng trắng thành 1 dấu cách → bỏ khoảng trắng
    /// và dấu câu (.,!?…) ở hai đầu → chữ thường (bất biến văn hóa; không ảnh hưởng chữ Hàn).
    /// </summary>
    public static string Normalize(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var normalized = WhitespaceRegex().Replace(text.Normalize(NormalizationForm.FormC), " ");
        normalized = normalized.Trim().Trim(PunctuationToTrim).Trim();
        return normalized.ToLower(CultureInfo.InvariantCulture);
    }

    private static readonly char[] PunctuationToTrim = ['.', ',', '!', '?', '…', '。', '、', '"', '\'', '“', '”'];
}
