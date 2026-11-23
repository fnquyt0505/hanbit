using FluentValidation.TestHelper;

using Hanbit.Domain.Content;
using Hanbit.Infrastructure.Content.Validation;

using Shouldly;

namespace Hanbit.Infrastructure.Tests.Content.Validation;

public class ContentValidatorsTests
{
    private static readonly SourceRef Source = new("한국어기초사전", "0001", "https://krdict.korean.go.kr/", "CC BY-SA 2.0 KR");

    private static Lesson LessonWith(params Exercise[] exercises) =>
        new("01-chao-hoi", "Chào hỏi", 1, [], [], exercises);

    [Fact]
    public void HangulLetterValidator_ValidLetter_HasNoErrors()
    {
        var letter = new HangulLetter("c-giyeok", "ㄱ", HangulType.Consonant, "g/k", "Như 'g'",
            [new HangulExample("가방", "gabang", "cái cặp")], NameKo: "기역");

        new HangulLetterValidator().TestValidate(letter).ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("가")]   // âm tiết, không phải chữ cái
    [InlineData("ㄱㄴ")] // 2 chữ cái
    [InlineData("g")]
    public void HangulLetterValidator_CharNotSingleJamo_HasError(string value)
    {
        var letter = new HangulLetter("c-x", value, HangulType.Consonant, "g", "x", [new HangulExample("가", "ga", "x")]);

        new HangulLetterValidator().TestValidate(letter).ShouldHaveValidationErrorFor(x => x.Char);
    }

    [Fact]
    public void HangulLetterValidator_NoExamples_HasError()
    {
        var letter = new HangulLetter("c-giyeok", "ㄱ", HangulType.Consonant, "g/k", "x", []);

        new HangulLetterValidator().TestValidate(letter).ShouldHaveValidationErrorFor(x => x.Examples);
    }

    [Theory]
    [InlineData("C-Giyeok")]
    [InlineData("c giyeok")]
    [InlineData("c--giyeok")]
    public void HangulLetterValidator_IdNotSlug_HasError(string id)
    {
        var letter = new HangulLetter(id, "ㄱ", HangulType.Consonant, "g/k", "x", [new HangulExample("가", "ga", "x")]);

        new HangulLetterValidator().TestValidate(letter).ShouldHaveValidationErrorFor(x => x.Id);
    }

    [Fact]
    public void VocabItemValidator_KoWithoutHangulAndBadUrl_HasErrors()
    {
        var item = new VocabItem("kd-1", "mul", "nước", PartOfSpeech.Noun, VocabGrade.Beginner, [], false,
            Source with { Url = "krdict" });

        var result = new VocabItemValidator().TestValidate(item);

        result.ShouldHaveValidationErrorFor(x => x.Ko);
        result.ShouldHaveValidationErrorFor("Source.Url");
    }

    [Fact]
    public void VocabItemValidator_EmptyOptionalString_HasError()
    {
        var item = new VocabItem("kd-1", "물", "nước", PartOfSpeech.Noun, VocabGrade.Beginner, [], false, Source, Hanja: " ");

        new VocabItemValidator().TestValidate(item).ShouldHaveValidationErrorFor(x => x.Hanja);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(2)]
    public void LessonValidator_AnswerIndexOutOfRange_HasErrorOnExercisePath(int answerIndex)
    {
        var lesson = LessonWith(new MultipleChoiceExercise("e1", "물?", ["nước", "lửa"], answerIndex));

        new LessonValidator().TestValidate(lesson).ShouldHaveValidationErrorFor("Exercises[0].AnswerIndex");
    }

    [Fact]
    public void LessonValidator_DuplicateChoices_HasError()
    {
        var lesson = LessonWith(new ListenChooseExercise("e1", "물", ["nước", "nước"], 0));

        new LessonValidator().TestValidate(lesson).ShouldHaveValidationErrorFor("Exercises[0].Choices");
    }

    [Fact]
    public void LessonValidator_ListenChooseWithoutHangul_HasError()
    {
        var lesson = LessonWith(new ListenChooseExercise("e1", "mul", ["nước", "lửa"], 0));

        new LessonValidator().TestValidate(lesson).ShouldHaveValidationErrorFor("Exercises[0].AudioKo");
    }

    [Fact]
    public void LessonValidator_MatchPairsWithDuplicateRight_HasError()
    {
        var lesson = LessonWith(new MatchPairsExercise("e1", "Nối", [new MatchPair("물", "nước"), new MatchPair("불", "nước")]));

        new LessonValidator().TestValidate(lesson).ShouldHaveValidationErrorFor("Exercises[0].Pairs");
    }

    [Fact]
    public void LessonValidator_ReorderWithOneToken_HasError()
    {
        var lesson = LessonWith(new ReorderExercise("e1", "Sắp xếp", ["물"]));

        new LessonValidator().TestValidate(lesson).ShouldHaveValidationErrorFor("Exercises[0].Tokens");
    }

    [Fact]
    public void LessonValidator_TypeAnswerWithoutAnswers_HasError()
    {
        var lesson = LessonWith(new TypeAnswerExercise("e1", "Gõ", []));

        new LessonValidator().TestValidate(lesson).ShouldHaveValidationErrorFor("Exercises[0].AcceptedAnswers");
    }

    [Fact]
    public void LessonValidator_DuplicateExerciseIds_HasError()
    {
        var lesson = LessonWith(
            new TypeAnswerExercise("e1", "Gõ", ["물"]),
            new TypeAnswerExercise("e1", "Gõ", ["불"]));

        new LessonValidator().TestValidate(lesson).ShouldHaveValidationErrorFor(x => x.Exercises);
    }

    [Fact]
    public void LessonValidator_ValidLesson_HasNoErrors()
    {
        var lesson = LessonWith(
            new MultipleChoiceExercise("e1", "물?", ["nước", "lửa"], 0, "물 = nước"),
            new ReorderExercise("e2", "Sắp xếp", ["물을", "마셔요."]));

        new LessonValidator().TestValidate(lesson).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void CourseValidator_DuplicateLessonOrder_HasError()
    {
        var course = new Course("beginner", "Sơ cấp", VocabGrade.Beginner, "Mô tả",
            [new LessonSummary("01-a", "A", 1), new LessonSummary("02-b", "B", 1)]);

        new CourseValidator().TestValidate(course).Errors.ShouldContain(e => e.ErrorMessage.Contains("order"));
    }
}
