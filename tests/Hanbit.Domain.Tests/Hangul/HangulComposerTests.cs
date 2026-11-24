using Hanbit.Domain.Hangul;

using Shouldly;

namespace Hanbit.Domain.Tests.Hangul;

public class HangulComposerTests
{
    [Theory]
    [InlineData("ㄱ", "ㅏ", null, '가', "ga")]
    [InlineData("ㅎ", "ㅏ", "ㄴ", '한', "han")]
    [InlineData("ㅂ", "ㅣ", "ㅊ", '빛', "bit")]
    [InlineData("ㅇ", "ㅏ", null, '아', "a")]
    [InlineData("ㄲ", "ㅗ", "ㅊ", '꽃', "kkot")]
    [InlineData("ㅇ", "ㅘ", null, '와', "wa")]
    [InlineData("ㄷ", "ㅏ", "ㄺ", '닭', "dak")]
    [InlineData("ㅎ", "ㅣ", "ㅎ", '힣', "hit")]
    public void TryCompose_ValidJamo_ReturnsSyllableAndRomanization(string c, string v, string? f, char syllable, string romanization)
    {
        var (result, error) = HangulComposer.TryCompose(c, v, f);

        error.ShouldBeNull();
        result.ShouldNotBeNull();
        result.Syllable.ShouldBe(syllable);
        result.Romanization.ShouldBe(romanization);
        result.Initial.ShouldBe(c[0]);
        result.Vowel.ShouldBe(v[0]);
        result.Final.ShouldBe(f is null ? null : f[0]);
    }

    [Fact]
    public void TryCompose_EmptyFinal_IsTreatedAsNoFinal()
    {
        HangulComposer.TryCompose("ㄱ", "ㅏ", string.Empty).Result!.Syllable.ShouldBe('가');
    }

    [Theory]
    [InlineData(null, "ㅏ", null, "c")]
    [InlineData("ㅏ", "ㅏ", null, "c")]
    [InlineData("ㄳ", "ㅏ", null, "c")]
    [InlineData("ㄱㄱ", "ㅏ", null, "c")]
    [InlineData("g", "ㅏ", null, "c")]
    [InlineData("ㄱ", null, null, "v")]
    [InlineData("ㄱ", "ㄱ", null, "v")]
    [InlineData("ㄱ", "a", null, "v")]
    [InlineData("ㄱ", "ㅏ", "ㄸ", "f")]
    [InlineData("ㄱ", "ㅏ", "ㅃ", "f")]
    [InlineData("ㄱ", "ㅏ", "ㅉ", "f")]
    [InlineData("ㄱ", "ㅏ", "ㅏ", "f")]
    public void TryCompose_InvalidJamo_ReturnsErrorForField(string? c, string? v, string? f, string field)
    {
        var (result, error) = HangulComposer.TryCompose(c, v, f);

        result.ShouldBeNull();
        error.ShouldNotBeNull();
        error.Field.ShouldBe(field);
        error.Message.ShouldNotBeNullOrWhiteSpace();
    }

    [Fact]
    public void JamoLists_HaveUnicodeCounts()
    {
        HangulComposer.Initials.Count.ShouldBe(19);
        HangulComposer.Vowels.Count.ShouldBe(21);
        HangulComposer.Finals.Count.ShouldBe(27);
    }

    [Fact]
    public void TryCompose_AllCombinations_ProduceDistinctValidSyllables()
    {
        var syllables = new HashSet<char>();
        foreach (var c in HangulComposer.Initials)
        {
            foreach (var v in HangulComposer.Vowels)
            {
                foreach (var f in HangulComposer.Finals.Select(x => x.ToString()).Prepend(null))
                {
                    var (result, _) = HangulComposer.TryCompose(c.ToString(), v.ToString(), f);
                    result.ShouldNotBeNull();
                    HangulSyllable.IsSyllable(result.Syllable).ShouldBeTrue();
                    syllables.Add(result.Syllable);
                }
            }
        }

        syllables.Count.ShouldBe(11172);
    }
}
