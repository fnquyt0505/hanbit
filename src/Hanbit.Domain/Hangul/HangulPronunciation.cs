using Hanbit.Domain.Content;

namespace Hanbit.Domain.Hangul;

/// <summary>Chữ được đọc (TTS) để phát âm một chữ cái.</summary>
public static class HangulPronunciation
{
    /// <summary>
    /// Phụ âm → tên chữ (<c>name_ko</c>, VD ㄱ → 기역); nguyên âm → âm tiết với ㅇ câm (ㅏ → 아).
    /// Phụ âm thiếu tên → chính chữ cái.
    /// </summary>
    public static string SpeakText(HangulLetter letter) => letter.Type switch
    {
        HangulType.Vowel or HangulType.CompoundVowel => HangulSyllable.VowelSyllable(letter.Char[0]).ToString(),
        _ => letter.NameKo ?? letter.Char,
    };
}
