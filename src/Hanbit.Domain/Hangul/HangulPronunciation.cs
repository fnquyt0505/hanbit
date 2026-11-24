using Hanbit.Domain.Content;

namespace Hanbit.Domain.Hangul;

/// <summary>Chữ được đọc (TTS) để phát âm một chữ cái.</summary>
public static class HangulPronunciation
{
    /// <summary>
    /// Phụ âm (cơ bản và kép) → ghép với ㅏ (ㄱ → 가, ㄲ → 까); nguyên âm → âm tiết với ㅇ câm (ㅏ → 아).
    /// Người dùng chốt 2026-11-24 (câu hỏi 12): đọc phụ âm ghép với ㅏ thay cho tên chữ (기역).
    /// </summary>
    public static string SpeakText(HangulLetter letter) => letter.Type switch
    {
        HangulType.Vowel or HangulType.CompoundVowel => HangulSyllable.VowelSyllable(letter.Char[0]).ToString(),
        _ => HangulSyllable.ConsonantSyllable(letter.Char[0]).ToString(),
    };
}
