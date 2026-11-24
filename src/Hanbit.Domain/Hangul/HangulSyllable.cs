namespace Hanbit.Domain.Hangul;

/// <summary>
/// Âm tiết Hangul tách theo công thức Unicode: <c>âm tiết = 0xAC00 + (đầu × 21 + giữa) × 28 + cuối</c>.
/// Chỉ số theo thứ tự Unicode: 19 phụ âm đầu, 21 nguyên âm, 28 phụ âm cuối (0 = không có).
/// </summary>
public readonly record struct HangulSyllable(int Initial, int Medial, int Final)
{
    public const char First = '가';
    public const char Last = '힣';
    public const int InitialCount = 19;
    public const int MedialCount = 21;
    public const int FinalCount = 28;

    /// <summary>Phụ âm đầu (chữ cái tương thích) theo chỉ số.</summary>
    public const string Initials = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";

    /// <summary>Nguyên âm theo chỉ số.</summary>
    public const string Medials = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";

    /// <summary>Phụ âm cuối theo chỉ số (vị trí 0 = không có, dùng '\0').</summary>
    public const string Finals = "\0ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";

    /// <summary>Chỉ số của ㅇ trong <see cref="Initials"/> — phụ âm đầu câm.</summary>
    public const int SilentInitial = 11;

    public static bool IsSyllable(char c) => c is >= First and <= Last;

    /// <summary>
    /// Âm tiết đọc một nguyên âm đứng một mình: ㅇ câm + nguyên âm, VD ㅏ → 아, ㅘ → 와.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">Không phải nguyên âm (chữ cái tương thích).</exception>
    public static char VowelSyllable(char vowel)
    {
        var medial = Medials.IndexOf(vowel, StringComparison.Ordinal);
        if (medial < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(vowel), vowel, "Không phải nguyên âm Hangul (ㅏ…ㅣ).");
        }

        return new HangulSyllable(SilentInitial, medial, 0).Compose();
    }

    public static HangulSyllable Decompose(char syllable)
    {
        if (!IsSyllable(syllable))
        {
            throw new ArgumentOutOfRangeException(nameof(syllable), syllable, "Không phải âm tiết Hangul (가…힣).");
        }

        var index = syllable - First;
        return new HangulSyllable(index / (MedialCount * FinalCount), index % (MedialCount * FinalCount) / FinalCount, index % FinalCount);
    }

    public char Compose() => (char)(First + ((Initial * MedialCount) + Medial) * FinalCount + Final);

    public char InitialJamo => Initials[Initial];

    public char MedialJamo => Medials[Medial];

    /// <summary>Phụ âm cuối dạng chữ cái tương thích; '\0' nếu không có.</summary>
    public char FinalJamo => Finals[Final];
}
