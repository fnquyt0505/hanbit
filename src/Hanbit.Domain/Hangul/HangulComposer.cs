namespace Hanbit.Domain.Hangul;

/// <summary>Kết quả ghép âm: âm tiết + phiên âm Revised Romanization.</summary>
public sealed record ComposedSyllable(char Initial, char Vowel, char? Final, char Syllable, string Romanization);

/// <summary>Lỗi ghép âm — thông báo tiếng Việt hiển thị được cho người học.</summary>
public sealed record ComposeError(string Field, string Message);

/// <summary>
/// Ghép phụ âm đầu + nguyên âm (+ phụ âm cuối) thành âm tiết theo công thức Unicode
/// (<see cref="HangulSyllable"/>): <c>0xAC00 + (đầu × 21 + giữa) × 28 + cuối</c>.
/// Nhận chữ cái tương thích (ㄱ, ㅏ, ㄴ…) — loại có trên bàn phím và trong <c>hangul.json</c>.
/// </summary>
public static class HangulComposer
{
    /// <summary>19 phụ âm đầu hợp lệ.</summary>
    public static IReadOnlyList<char> Initials { get; } = [.. HangulSyllable.Initials];

    /// <summary>21 nguyên âm hợp lệ.</summary>
    public static IReadOnlyList<char> Vowels { get; } = [.. HangulSyllable.Medials];

    /// <summary>27 phụ âm cuối hợp lệ (gồm cả phụ âm cuối kép như ㄳ, ㄺ).</summary>
    public static IReadOnlyList<char> Finals { get; } = [.. HangulSyllable.Finals[1..]];

    /// <summary>Ghép âm; trả về lỗi (thay vì ném exception) khi chữ cái không hợp lệ ở vị trí đó.</summary>
    public static (ComposedSyllable? Result, ComposeError? Error) TryCompose(string? initial, string? vowel, string? final = null)
    {
        if (!TrySingleJamo(initial, HangulSyllable.Initials, out var initialIndex))
        {
            return (null, new ComposeError("c", $"'{initial}' không phải phụ âm đầu hợp lệ (ㄱ…ㅎ, gồm cả phụ âm kép ㄲ ㄸ ㅃ ㅆ ㅉ)."));
        }

        if (!TrySingleJamo(vowel, HangulSyllable.Medials, out var vowelIndex))
        {
            return (null, new ComposeError("v", $"'{vowel}' không phải nguyên âm hợp lệ (ㅏ…ㅣ)."));
        }

        var finalIndex = 0;
        if (!string.IsNullOrEmpty(final))
        {
            if (!TrySingleJamo(final, HangulSyllable.Finals, out finalIndex) || finalIndex == 0)
            {
                return (null, new ComposeError("f", $"'{final}' không thể làm phụ âm cuối (ㄸ ㅃ ㅉ không đứng cuối âm tiết)."));
            }
        }

        var syllable = new HangulSyllable(initialIndex, vowelIndex, finalIndex).Compose();
        return (new ComposedSyllable(
            HangulSyllable.Initials[initialIndex],
            HangulSyllable.Medials[vowelIndex],
            finalIndex == 0 ? null : HangulSyllable.Finals[finalIndex],
            syllable,
            RomanizationService.Romanize(syllable.ToString())), null);
    }

    private static bool TrySingleJamo(string? value, string table, out int index)
    {
        index = value is { Length: 1 } ? table.IndexOf(value[0], StringComparison.Ordinal) : -1;
        return index >= 0;
    }
}
