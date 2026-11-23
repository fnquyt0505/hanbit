namespace Hanbit.Domain.Content;

public enum HangulType
{
    Consonant,
    Vowel,
    DoubleConsonant,
    CompoundVowel,
}

/// <summary>Một chữ cái Hangul (content/hangul.json). Trường có <c>= null</c> được bỏ trống trong JSON.</summary>
/// <param name="Id">Id ổn định, VD <c>c-giyeok</c>.</param>
/// <param name="Char">Ký tự, VD <c>ㄱ</c>.</param>
/// <param name="Romanization">Phiên âm Revised Romanization, VD <c>g/k</c>.</param>
/// <param name="ViHint">Gợi ý cách đọc cho người Việt.</param>
/// <param name="NameKo">Tên chữ bằng tiếng Hàn, VD <c>기역</c> (nguyên âm không có tên riêng → null).</param>
/// <param name="Audio">Đường dẫn MP3 dự phòng (null → dùng Web Speech API).</param>
public sealed record HangulLetter(
    string Id,
    string Char,
    HangulType Type,
    string Romanization,
    string ViHint,
    IReadOnlyList<HangulExample> Examples,
    string? NameKo = null,
    string? Audio = null);

/// <summary>Từ ví dụ minh họa cho một chữ cái.</summary>
public sealed record HangulExample(string Ko, string Romanization, string Vi);
