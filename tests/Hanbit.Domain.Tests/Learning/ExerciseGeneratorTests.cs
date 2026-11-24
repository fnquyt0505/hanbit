using Hanbit.Domain.Content;
using Hanbit.Domain.Learning;

using Shouldly;

namespace Hanbit.Domain.Tests.Learning;

public class ExerciseGeneratorTests
{
    private static VocabItem Word(string id, string ko, string vi) =>
        new(id, ko, vi, PartOfSpeech.Noun, VocabGrade.Beginner, [], Reviewed: true,
            new SourceRef("krdict", id, "https://krdict.korean.go.kr", "CC BY-SA 2.0 KR"));

    private static readonly IReadOnlyList<VocabItem> Family =
    [
        Word("kd-1", "가족", "gia đình"),
        Word("kd-2", "아버지", "cha, ba, bố; ba…, bố…; cha đẻ"),
        Word("kd-3", "어머니", "người mẹ, mẹ; má; cội nguồn"),
        Word("kd-4", "형", "anh"),
        Word("kd-5", "오빠", "anh"),
        Word("kd-6", "누나", "chị"),
        Word("kd-7", "딸", "con gái, đứa con gái"),
        Word("kd-8", "아들", "con trai"),
    ];

    private static string Flatten(IEnumerable<Exercise> exercises) => string.Join("\n", exercises.Select(e => e switch
    {
        MultipleChoiceExercise mc => $"{mc.Id}|{mc.Prompt}|{string.Join(",", mc.Choices)}|{mc.AnswerIndex}",
        ListenChooseExercise lc => $"{lc.Id}|{lc.AudioKo}|{string.Join(",", lc.Choices)}|{lc.AnswerIndex}",
        MatchPairsExercise mp => $"{mp.Id}|{string.Join(",", mp.Pairs)}",
        _ => e.Id,
    }));

    [Fact]
    public void Generate_SameLesson_IsDeterministic()
    {
        Flatten(ExerciseGenerator.Generate("beginner", "gia-dinh", Family))
            .ShouldBe(Flatten(ExerciseGenerator.Generate("beginner", "gia-dinh", Family)));
    }

    [Fact]
    public void Generate_DifferentLesson_ProducesDifferentSet()
    {
        Flatten(ExerciseGenerator.Generate("beginner", "a", Family))
            .ShouldNotBe(Flatten(ExerciseGenerator.Generate("beginner", "b", Family)));
    }

    [Fact]
    public void Generate_SevenExercises_WithUniqueGenIds()
    {
        var exercises = ExerciseGenerator.Generate("beginner", "gia-dinh", Family);

        exercises.Count.ShouldBe(7);
        exercises.OfType<MultipleChoiceExercise>().Count().ShouldBe(4);
        exercises.OfType<ListenChooseExercise>().Count().ShouldBe(2);
        exercises.OfType<MatchPairsExercise>().Count().ShouldBe(1);
        exercises.Select(e => e.Id).ShouldBeUnique();
        exercises.ShouldAllBe(e => e.Id.StartsWith(ExerciseGenerator.IdPrefix));
    }

    [Fact]
    public void Generate_ChoiceExercises_HaveFourDistinctChoices_AnswerHasNoSameMeaningDistractor()
    {
        foreach (var lesson in new[] { "a", "b", "c", "d", "e", "f" })
        {
            foreach (var exercise in ExerciseGenerator.Generate("beginner", lesson, Family))
            {
                var (choices, answerIndex) = exercise switch
                {
                    MultipleChoiceExercise mc => (mc.Choices, mc.AnswerIndex),
                    ListenChooseExercise lc => (lc.Choices, lc.AnswerIndex),
                    _ => (null, -1),
                };
                if (choices is null)
                {
                    continue;
                }

                choices.Count.ShouldBe(4);
                choices.ShouldBeUnique();
                answerIndex.ShouldBeInRange(0, 3);

                // 형/오빠 đều nghĩa "anh": không bao giờ cùng xuất hiện như đáp án + nhiễu
                if (choices[answerIndex] is "형" or "오빠")
                {
                    choices.ShouldNotContain(choices[answerIndex] == "형" ? "오빠" : "형");
                }
            }
        }
    }

    [Fact]
    public void Generate_MeaningChoices_UseFirstMeaningOnly()
    {
        var meanings = ExerciseGenerator.Generate("beginner", "x", Family)
            .OfType<ListenChooseExercise>().SelectMany(l => l.Choices);

        meanings.ShouldAllBe(m => !m.Contains(';'));
    }

    [Fact]
    public void Generate_MatchPairs_HaveUniqueSides()
    {
        var match = ExerciseGenerator.Generate("beginner", "gia-dinh", Family).OfType<MatchPairsExercise>().Single();

        match.Pairs.Count.ShouldBe(5);
        match.Pairs.Select(p => p.Left).ShouldBeUnique();
        match.Pairs.Select(p => p.Right).ShouldBeUnique();
    }

    [Fact]
    public void Generate_TooFewDistinctMeanings_ReturnsEmpty()
    {
        ExerciseGenerator.Generate("c", "l", [Family[0], Family[3], Family[4]]).ShouldBeEmpty();
    }

    [Theory]
    [InlineData("gen-nghia-kd-123", "kd-123")]
    [InlineData("gen-tu-kd-44487", "kd-44487")]
    [InlineData("gen-nghe-kd-1", "kd-1")]
    [InlineData("gen-noi-cap", null)]
    [InlineData("nghia-gamsahamnida", null)]
    public void VocabIdFor_ExtractsWordFromSingleWordGeneratedIds(string exerciseId, string? expected)
    {
        ExerciseGenerator.VocabIdFor(exerciseId).ShouldBe(expected);
    }

    [Fact]
    public void VocabIdFor_EveryGeneratedChoiceExercise_PointsToItsWord()
    {
        foreach (var exercise in ExerciseGenerator.Generate("beginner", "gia-dinh", Family).Where(e => e is not MatchPairsExercise))
        {
            var id = ExerciseGenerator.VocabIdFor(exercise.Id);
            id.ShouldNotBeNull(exercise.Id);
            Family.Select(f => f.Id).ShouldContain(id);
        }
    }

    [Theory]
    [InlineData("cha, ba, bố; ba…, bố…; cha đẻ", "cha, ba, bố")]
    [InlineData("gia đình", "gia đình")]
    [InlineData(" anh ; em ", "anh")]
    public void ShortMeaning_TakesFirstSense(string vi, string expected)
    {
        ExerciseGenerator.ShortMeaning(vi).ShouldBe(expected);
    }

    [Fact]
    public void StableSeed_IsFixedAcrossRuns()
    {
        // Giá trị cố định — đổi thuật toán sẽ làm đổi bộ bài tập đã sinh của mọi bài học.
        ExerciseGenerator.StableSeed("beginner/chao-hoi").ShouldBe(ExerciseGenerator.StableSeed("beginner/chao-hoi"));
        ExerciseGenerator.StableSeed("").ShouldBe(unchecked((int)(2166136261u & 0x7FFFFFFF)));
    }
}
