using Hanbit.Domain.Hangul;

using Shouldly;

namespace Hanbit.Domain.Tests.Hangul;

public class HangulSyllableTests
{
    [Theory]
    [InlineData('ㅏ', '아')]
    [InlineData('ㅑ', '야')]
    [InlineData('ㅓ', '어')]
    [InlineData('ㅗ', '오')]
    [InlineData('ㅜ', '우')]
    [InlineData('ㅡ', '으')]
    [InlineData('ㅣ', '이')]
    [InlineData('ㅐ', '애')]
    [InlineData('ㅘ', '와')]
    [InlineData('ㅝ', '워')]
    [InlineData('ㅢ', '의')]
    public void VowelSyllable_AddsSilentInitial(char vowel, char expected)
    {
        HangulSyllable.VowelSyllable(vowel).ShouldBe(expected);
    }

    [Theory]
    [InlineData('ㄱ')]
    [InlineData('가')]
    [InlineData('a')]
    public void VowelSyllable_NotAVowel_Throws(char notVowel)
    {
        Should.Throw<ArgumentOutOfRangeException>(() => HangulSyllable.VowelSyllable(notVowel));
    }

    [Theory]
    [InlineData('가', 'ㄱ', 'ㅏ', '\0')]
    [InlineData('한', 'ㅎ', 'ㅏ', 'ㄴ')]
    [InlineData('빛', 'ㅂ', 'ㅣ', 'ㅊ')]
    [InlineData('힣', 'ㅎ', 'ㅣ', 'ㅎ')]
    public void Decompose_ThenCompose_RoundTrips(char syllable, char initial, char medial, char final)
    {
        var parts = HangulSyllable.Decompose(syllable);

        parts.InitialJamo.ShouldBe(initial);
        parts.MedialJamo.ShouldBe(medial);
        parts.FinalJamo.ShouldBe(final);
        parts.Compose().ShouldBe(syllable);
    }
}
