using Hanbit.Domain.Content;
using Hanbit.Domain.Learning;

using Shouldly;

namespace Hanbit.Domain.Tests.Learning;

public class AnswerCheckerTests
{
    private static readonly MultipleChoiceExercise Mc = new("mc", "\"감사합니다\" nghĩa là gì?", ["Xin lỗi", "Cảm ơn", "Xin chào"], 1, "giải thích");
    private static readonly ListenChooseExercise Listen = new("lc", "죄송합니다", ["Cảm ơn", "Xin lỗi"], 1);
    private static readonly MatchPairsExercise Match = new("mp", "Nối", [new("이름", "tên"), new("성", "họ"), new("친구", "bạn")]);
    private static readonly ReorderExercise Reorder = new("ro", "Sắp xếp", ["저는", "학생이", "아니에요."]);
    private static readonly TypeAnswerExercise Type = new("ta", "Gõ", ["반갑습니다", "반갑습니다."]);

    [Theory]
    [InlineData(1, true)]
    [InlineData(0, false)]
    [InlineData(2, false)]
    [InlineData(99, false)]
    public void MultipleChoice_ComparesIndex(int index, bool correct)
    {
        var result = AnswerChecker.Check(Mc, new ChoiceAnswer(index));

        result.ShouldNotBeNull();
        result.Correct.ShouldBe(correct);
        result.CorrectAnswer.ShouldBe("Cảm ơn");
        result.ExplanationVi.ShouldBe("giải thích");
    }

    [Fact]
    public void ListenChoose_ComparesIndex()
    {
        AnswerChecker.Check(Listen, new ChoiceAnswer(1))!.Correct.ShouldBeTrue();
        AnswerChecker.Check(Listen, new ChoiceAnswer(0))!.CorrectAnswer.ShouldBe("Xin lỗi");
    }

    [Fact]
    public void MatchPairs_AllPairsInAnyOrder_IsCorrect()
    {
        var answer = new PairsAnswer([new("친구", "bạn"), new("이름", "tên"), new("성", "họ")]);

        var result = AnswerChecker.Check(Match, answer);

        result!.Correct.ShouldBeTrue();
        result.CorrectAnswer.ShouldBe("이름 — tên; 성 — họ; 친구 — bạn");
    }

    [Fact]
    public void MatchPairs_OneSwapped_IsWrong()
    {
        AnswerChecker.Check(Match, new PairsAnswer([new("이름", "họ"), new("성", "tên"), new("친구", "bạn")]))!.Correct.ShouldBeFalse();
    }

    [Fact]
    public void MatchPairs_MissingPair_IsWrong()
    {
        AnswerChecker.Check(Match, new PairsAnswer([new("이름", "tên"), new("성", "họ")]))!.Correct.ShouldBeFalse();
    }

    [Fact]
    public void Reorder_CorrectOrder_IsCorrect_WrongOrder_IsWrong()
    {
        AnswerChecker.Check(Reorder, new OrderAnswer(["저는", "학생이", "아니에요."]))!.Correct.ShouldBeTrue();
        AnswerChecker.Check(Reorder, new OrderAnswer(["학생이", "저는", "아니에요."]))!.Correct.ShouldBeFalse();
        AnswerChecker.Check(Reorder, new OrderAnswer(["저는", "학생이"]))!.Correct.ShouldBeFalse();
        AnswerChecker.Check(Reorder, new OrderAnswer(["저는"]))!.CorrectAnswer.ShouldBe("저는 학생이 아니에요.");
    }

    [Theory]
    [InlineData("반갑습니다", true)]
    [InlineData("  반갑습니다!  ", true)]
    [InlineData("반갑습니다.", true)]
    [InlineData("반갑 습니다", false)]
    [InlineData("", false)]
    [InlineData("감사합니다", false)]
    public void TypeAnswer_NormalizesBeforeComparing(string text, bool correct)
    {
        AnswerChecker.Check(Type, new TextAnswer(text))!.Correct.ShouldBe(correct);
    }

    [Fact]
    public void TypeAnswer_DecomposedJamoInput_MatchesAfterNfc()
    {
        // 반 gõ bằng jamo tách rời (NFD): ᄇ + ᅡ + ᆫ
        var decomposed = "반갑습니다".Normalize(System.Text.NormalizationForm.FormD);
        decomposed.ShouldNotBe("반갑습니다");

        AnswerChecker.Check(Type, new TextAnswer(decomposed))!.Correct.ShouldBeTrue();
    }

    [Fact]
    public void MismatchedAnswerKind_ReturnsNull()
    {
        AnswerChecker.Check(Mc, new TextAnswer("Cảm ơn")).ShouldBeNull();
        AnswerChecker.Check(Type, new ChoiceAnswer(0)).ShouldBeNull();
    }

    [Theory]
    [InlineData("  Xin   CHÀO ", "xin chào")]
    [InlineData("vào thứ Sáu.", "vào thứ sáu")]
    [InlineData("\"제\"", "제")]
    [InlineData(null, "")]
    public void TextNormalizer_Normalize(string? input, string expected)
    {
        TextNormalizer.Normalize(input).ShouldBe(expected);
    }
}
