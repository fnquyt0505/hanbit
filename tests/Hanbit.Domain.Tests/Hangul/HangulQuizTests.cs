using Hanbit.Domain.Content;
using Hanbit.Domain.Hangul;

using Shouldly;

namespace Hanbit.Domain.Tests.Hangul;

public class HangulQuizTests
{
    private static HangulLetter Consonant(string id, string ch, string rom, string name) =>
        new(id, ch, HangulType.Consonant, rom, "hint", [], NameKo: name);

    private static HangulLetter Vowel(string id, string ch, string rom) =>
        new(id, ch, HangulType.Vowel, rom, "hint", []);

    private static readonly IReadOnlyList<HangulLetter> Vowels =
    [
        Vowel("v-a", "ㅏ", "a"), Vowel("v-ya", "ㅑ", "ya"), Vowel("v-eo", "ㅓ", "eo"),
        Vowel("v-yeo", "ㅕ", "yeo"), Vowel("v-o", "ㅗ", "o"), Vowel("v-yo", "ㅛ", "yo"),
    ];

    private static readonly IReadOnlyList<HangulLetter> Doubles =
    [
        Consonant("d-kk", "ㄲ", "kk", "쌍기역"), Consonant("d-tt", "ㄸ", "tt", "쌍디귿"),
        Consonant("d-pp", "ㅃ", "pp", "쌍비읍"), Consonant("d-ss", "ㅆ", "ss", "쌍시옷"),
        Consonant("d-jj", "ㅉ", "jj", "쌍지읒"),
    ];

    [Theory]
    [InlineData(1)]
    [InlineData(42)]
    [InlineData(2026)]
    public void Generate_TenQuestions_EachWithFourDistinctChoicesIncludingAnswer(int seed)
    {
        var questions = HangulQuiz.Generate(Vowels, seed);

        questions.Count.ShouldBe(10);
        foreach (var q in questions)
        {
            q.Choices.Count.ShouldBe(4);
            q.Choices.ShouldBeUnique();
            q.Choices.ShouldContain(q.Answer);
        }
    }

    [Fact]
    public void Generate_SameSeed_IsDeterministic_DifferentSeed_Differs()
    {
        static List<string> Flatten(IEnumerable<HangulQuizQuestion> quiz) =>
            [.. quiz.Select(q => $"{q.Kind}|{q.Prompt}|{string.Join(",", q.Choices)}|{q.Answer}")];

        var a = Flatten(HangulQuiz.Generate(Vowels, 7));
        var b = Flatten(HangulQuiz.Generate(Vowels, 7));
        var c = Flatten(HangulQuiz.Generate(Vowels, 8));

        a.ShouldBe(b);
        c.ShouldNotBe(a);
    }

    [Fact]
    public void Generate_UsesEveryLetterBeforeRepeating()
    {
        var questions = HangulQuiz.Generate(Doubles, 3);

        // 5 chữ, 10 câu → mỗi chữ đúng 2 lần
        var answersAsChar = questions.Select(q => q.Kind == HangulQuizKind.CharToRomanization ? q.Prompt : q.Answer);
        answersAsChar.Take(5).ShouldBeUnique();
        answersAsChar.GroupBy(c => c).ShouldAllBe(g => g.Count() == 2);
    }

    [Fact]
    public void Generate_MixesAllThreeKinds()
    {
        HangulQuiz.Generate(Vowels, 5).Select(q => q.Kind).Distinct().Count().ShouldBe(3);
    }

    [Fact]
    public void Generate_ListenQuestion_PromptIsSpeakText()
    {
        var listen = HangulQuiz.Generate(Doubles, 11).First(q => q.Kind == HangulQuizKind.ListenToChar);
        var letter = Doubles.Single(l => l.Char == listen.Answer);

        listen.Prompt.ShouldBe(letter.NameKo);
    }

    [Fact]
    public void Generate_EveryGeneratedQuestion_ChecksCorrectWithItsAnswer()
    {
        foreach (var q in HangulQuiz.Generate(Vowels, 99, count: 30))
        {
            var check = HangulQuiz.Check(Vowels, q.Kind, q.Prompt, q.Answer);
            check.ShouldNotBeNull();
            check.Correct.ShouldBeTrue();
            check.CorrectAnswer.ShouldBe(q.Answer);
        }
    }

    [Theory]
    [InlineData(HangulQuizKind.CharToRomanization, "ㅏ", "a", true, "a")]
    [InlineData(HangulQuizKind.CharToRomanization, "ㅏ", "ya", false, "a")]
    [InlineData(HangulQuizKind.RomanizationToChar, "eo", "ㅓ", true, "ㅓ")]
    [InlineData(HangulQuizKind.RomanizationToChar, "eo", "ㅗ", false, "ㅓ")]
    [InlineData(HangulQuizKind.ListenToChar, "아", "ㅏ", true, "ㅏ")]
    [InlineData(HangulQuizKind.ListenToChar, "요", "ㅕ", false, "ㅛ")]
    public void Check_ReturnsCorrectnessAndCorrectAnswer(HangulQuizKind kind, string prompt, string answer, bool correct, string expectedAnswer)
    {
        var check = HangulQuiz.Check(Vowels, kind, prompt, answer);

        check.ShouldNotBeNull();
        check.Correct.ShouldBe(correct);
        check.CorrectAnswer.ShouldBe(expectedAnswer);
    }

    [Fact]
    public void Check_UnknownPrompt_ReturnsNull()
    {
        HangulQuiz.Check(Vowels, HangulQuizKind.CharToRomanization, "ㄱ", "g/k").ShouldBeNull();
    }

    [Fact]
    public void Generate_TooFewLetters_Throws()
    {
        Should.Throw<ArgumentOutOfRangeException>(() => HangulQuiz.Generate([Vowels[0]], 1));
    }
}
