using System.Text.Json;

using Hanbit.Domain.Content;

using Shouldly;

namespace Hanbit.Infrastructure.Tests.Content;

/// <summary>Kiểm tra dữ liệu thật trong content/hangul.json (T1.2).</summary>
public class HangulDataTests
{
    private static readonly IReadOnlyList<HangulLetter> Letters = Load();

    private static IReadOnlyList<HangulLetter> Load()
    {
        var path = Path.Combine(RepoRoot.Find(), "content", "hangul.json");
        return JsonSerializer.Deserialize<List<HangulLetter>>(File.ReadAllText(path), ContentJson.Options)
            ?? throw new InvalidOperationException("hangul.json rỗng.");
    }

    [Theory]
    [InlineData(HangulType.Consonant, 14)]
    [InlineData(HangulType.DoubleConsonant, 5)]
    [InlineData(HangulType.Vowel, 10)]
    [InlineData(HangulType.CompoundVowel, 11)]
    public void HangulJson_EachType_HasExpectedCount(HangulType type, int expected)
    {
        Letters.Count(l => l.Type == type).ShouldBe(expected);
    }

    [Fact]
    public void HangulJson_Total_Is40()
    {
        Letters.Count.ShouldBe(40);
    }

    [Fact]
    public void HangulJson_IdsAndChars_AreUnique()
    {
        Letters.Select(l => l.Id).ShouldBeUnique();
        Letters.Select(l => l.Char).ShouldBeUnique();
    }

    [Fact]
    public void HangulJson_EveryChar_IsSingleCompatibilityJamo()
    {
        foreach (var letter in Letters)
        {
            letter.Char.Length.ShouldBe(1, letter.Id);
            Jamo.IsCompatibilityJamo(letter.Char[0]).ShouldBeTrue(letter.Id);
        }
    }

    [Fact]
    public void HangulJson_Consonants_HaveKoreanName_VowelsDoNot()
    {
        foreach (var letter in Letters)
        {
            var isConsonant = letter.Type is HangulType.Consonant or HangulType.DoubleConsonant;
            (letter.NameKo is not null).ShouldBe(isConsonant, letter.Id);
        }
    }

    [Fact]
    public void HangulJson_EveryLetter_HasHintAndExample()
    {
        foreach (var letter in Letters)
        {
            letter.ViHint.ShouldNotBeNullOrWhiteSpace(letter.Id);
            letter.Romanization.ShouldNotBeNullOrWhiteSpace(letter.Id);
            letter.Examples.ShouldNotBeEmpty(letter.Id);
        }
    }

    [Fact]
    public void HangulJson_EveryExample_ContainsTheLetter()
    {
        foreach (var letter in Letters)
        {
            foreach (var example in letter.Examples)
            {
                Jamo.Decompose(example.Ko).ShouldContain(letter.Char[0],
                    $"{letter.Id}: '{example.Ko}' không chứa {letter.Char}");
            }
        }
    }

    [Theory]
    [InlineData("가방", "ㄱㅏㅂㅏㅇ")]
    [InlineData("의사", "ㅇㅢㅅㅏ")]
    [InlineData("쌀", "ㅆㅏㄹ")]
    public void Decompose_Syllables_ReturnsCompatibilityJamo(string text, string expected)
    {
        new string(Jamo.Decompose(text).ToArray()).ShouldBe(expected);
    }

    /// <summary>Tách âm tiết Hangul (U+AC00–U+D7A3) thành chữ cái tương thích (ㄱ, ㅏ…) theo công thức Unicode.</summary>
    private static class Jamo
    {
        private const string Initials = "ㄱㄲㄴㄷㄸㄹㅁㅂㅃㅅㅆㅇㅈㅉㅊㅋㅌㅍㅎ";
        private const string Medials = "ㅏㅐㅑㅒㅓㅔㅕㅖㅗㅘㅙㅚㅛㅜㅝㅞㅟㅠㅡㅢㅣ";
        private const string Finals = "\0ㄱㄲㄳㄴㄵㄶㄷㄹㄺㄻㄼㄽㄾㄿㅀㅁㅂㅄㅅㅆㅇㅈㅊㅋㅌㅍㅎ";

        public static bool IsCompatibilityJamo(char c) => c is >= 'ㄱ' and <= 'ㅣ';

        public static IEnumerable<char> Decompose(string text)
        {
            foreach (var c in text)
            {
                if (c is < '가' or > '힣')
                {
                    yield return c;
                    continue;
                }

                var index = c - '가';
                yield return Initials[index / (21 * 28)];
                yield return Medials[index % (21 * 28) / 28];
                if (index % 28 != 0)
                {
                    yield return Finals[index % 28];
                }
            }
        }
    }
}
