using System.Text.Json;

using Hanbit.Domain.Content;

using Shouldly;

namespace Hanbit.Domain.Tests.Content;

public class ContentJsonTests
{
    private static T Deserialize<T>(string json) =>
        JsonSerializer.Deserialize<T>(json, ContentJson.Options)
        ?? throw new InvalidOperationException("JSON trả về null.");

    [Fact]
    public void Deserialize_HangulLetter_MapsSnakeCaseAndEnum()
    {
        const string json = """
            {
              "id": "c-giyeok",
              "char": "ㄱ",
              "type": "consonant",
              "romanization": "g/k",
              "name_ko": "기역",
              "vi_hint": "Giữa 'c' và 'g'",
              "examples": [ { "ko": "가", "romanization": "ga", "vi": "chữ ghép ㄱ + ㅏ" } ]
            }
            """;

        var letter = Deserialize<HangulLetter>(json);

        letter.Id.ShouldBe("c-giyeok");
        letter.Char.ShouldBe("ㄱ");
        letter.Type.ShouldBe(HangulType.Consonant);
        letter.NameKo.ShouldBe("기역");
        letter.Examples.ShouldHaveSingleItem().Romanization.ShouldBe("ga");
        letter.Audio.ShouldBeNull();
    }

    [Theory]
    [InlineData("double_consonant", HangulType.DoubleConsonant)]
    [InlineData("compound_vowel", HangulType.CompoundVowel)]
    public void Deserialize_HangulType_UsesSnakeCaseValues(string value, HangulType expected)
    {
        var json = $$"""
            { "id": "x", "char": "x", "type": "{{value}}", "romanization": "x", "vi_hint": "x", "examples": [] }
            """;

        Deserialize<HangulLetter>(json).Type.ShouldBe(expected);
    }

    [Fact]
    public void Deserialize_VocabItem_ReadsSourceRefAndOptionalFields()
    {
        const string json = """
            {
              "id": "kd-0001",
              "ko": "물",
              "vi": "nước",
              "pos": "noun",
              "grade": "beginner",
              "examples": [ { "ko": "물을 마셔요.", "vi": "Tôi uống nước." } ],
              "reviewed": false,
              "source": {
                "name": "한국어기초사전",
                "id": "0001",
                "url": "https://krdict.korean.go.kr/",
                "license": "CC BY-SA 2.0 KR"
              },
              "pronunciation": "[물]",
            }
            """;

        var item = Deserialize<VocabItem>(json);

        item.Ko.ShouldBe("물");
        item.Pos.ShouldBe(PartOfSpeech.Noun);
        item.Grade.ShouldBe(VocabGrade.Beginner);
        item.Reviewed.ShouldBeFalse();
        item.Source.Name.ShouldBe("한국어기초사전");
        item.Source.License.ShouldBe("CC BY-SA 2.0 KR");
        item.Pronunciation.ShouldBe("[물]");
        item.Hanja.ShouldBeNull();
    }

    [Fact]
    public void Deserialize_Lesson_ReadsAllExerciseTypes()
    {
        const string json = """
            {
              // Bài học mẫu — comment và dấu phẩy cuối được phép trong file viết tay
              "slug": "01-chao-hoi",
              "title": "Chào hỏi",
              "order": 1,
              "vocab_ids": ["kd-0001"],
              "grammar_ids": [],
              "exercises": [
                { "type": "multiple_choice", "id": "e1", "prompt": "물 nghĩa là gì?", "choices": ["nước", "lửa"], "answer_index": 0, "explanation_vi": "물 = nước" },
                { "type": "listen_choose", "id": "e2", "audio_ko": "물", "choices": ["nước", "lửa"], "answer_index": 0 },
                { "type": "match_pairs", "id": "e3", "prompt": "Nối từ", "pairs": [ { "left": "물", "right": "nước" } ] },
                { "type": "reorder", "id": "e4", "prompt": "Sắp xếp câu", "tokens": ["물을", "마셔요."] },
                { "type": "type_answer", "id": "e5", "prompt": "Gõ 'xin chào' bằng tiếng Hàn", "accepted_answers": ["안녕하세요"] },
              ],
            }
            """;

        var lesson = Deserialize<Lesson>(json);

        lesson.Slug.ShouldBe("01-chao-hoi");
        lesson.Exercises.Count.ShouldBe(5);
        var multipleChoice = lesson.Exercises[0].ShouldBeOfType<MultipleChoiceExercise>();
        multipleChoice.AnswerIndex.ShouldBe(0);
        multipleChoice.ExplanationVi.ShouldBe("물 = nước");
        lesson.Exercises[1].ShouldBeOfType<ListenChooseExercise>().AudioKo.ShouldBe("물");
        lesson.Exercises[2].ShouldBeOfType<MatchPairsExercise>().Pairs.ShouldHaveSingleItem().Right.ShouldBe("nước");
        lesson.Exercises[3].ShouldBeOfType<ReorderExercise>().Tokens.ShouldBe(["물을", "마셔요."]);
        var typeAnswer = lesson.Exercises[4].ShouldBeOfType<TypeAnswerExercise>();
        typeAnswer.AcceptedAnswers.ShouldBe(["안녕하세요"]);
        typeAnswer.ExplanationVi.ShouldBeNull();
    }

    [Fact]
    public void Deserialize_ExerciseWithTypeNotFirst_StillResolvesType()
    {
        const string json = """
            { "id": "e1", "prompt": "?", "choices": ["a", "b"], "answer_index": 1, "type": "multiple_choice" }
            """;

        Deserialize<Exercise>(json).ShouldBeOfType<MultipleChoiceExercise>().AnswerIndex.ShouldBe(1);
    }

    [Fact]
    public void Deserialize_Course_ReadsLessonSummaries()
    {
        const string json = """
            {
              "slug": "beginner",
              "title": "Sơ cấp",
              "level": "beginner",
              "description": "Khóa sơ cấp",
              "lessons": [ { "slug": "01-chao-hoi", "title": "Chào hỏi", "order": 1 } ]
            }
            """;

        var course = Deserialize<Course>(json);

        course.Level.ShouldBe(VocabGrade.Beginner);
        course.Lessons.ShouldHaveSingleItem().Slug.ShouldBe("01-chao-hoi");
    }

    [Fact]
    public void Deserialize_GrammarPoint_ReadsFields()
    {
        const string json = """
            {
              "id": "g-01",
              "pattern": "-요",
              "meaning_vi": "Đuôi câu lịch sự",
              "explanation_vi": "Thêm vào cuối câu để nói lịch sự.",
              "examples": [],
              "grade": "beginner"
            }
            """;

        Deserialize<GrammarPoint>(json).Pattern.ShouldBe("-요");
    }

    [Fact]
    public void Deserialize_UnknownExerciseType_Throws()
    {
        const string json = """{ "type": "fill_blank", "id": "e1" }""";

        Should.Throw<JsonException>(() => Deserialize<Exercise>(json));
    }

    [Fact]
    public void Deserialize_MissingRequiredProperty_Throws()
    {
        // thiếu "vi_hint"
        const string json = """
            { "id": "x", "char": "x", "type": "vowel", "romanization": "a", "examples": [] }
            """;

        Should.Throw<JsonException>(() => Deserialize<HangulLetter>(json));
    }

    [Fact]
    public void Deserialize_NullForNonNullableProperty_Throws()
    {
        const string json = """
            { "id": "x", "char": "x", "type": "vowel", "romanization": null, "vi_hint": "x", "examples": [] }
            """;

        Should.Throw<JsonException>(() => Deserialize<HangulLetter>(json));
    }

    [Fact]
    public void Deserialize_UnknownProperty_Throws()
    {
        // gõ sai "vi_hint" thành "vi_hnt"
        const string json = """
            { "id": "x", "char": "x", "type": "vowel", "romanization": "a", "vi_hint": "x", "vi_hnt": "x", "examples": [] }
            """;

        Should.Throw<JsonException>(() => Deserialize<HangulLetter>(json));
    }

    [Fact]
    public void Deserialize_EnumAsInteger_Throws()
    {
        const string json = """
            { "id": "x", "char": "x", "type": 1, "romanization": "a", "vi_hint": "x", "examples": [] }
            """;

        Should.Throw<JsonException>(() => Deserialize<HangulLetter>(json));
    }

    [Fact]
    public void Serialize_VocabItem_WritesSnakeCaseAndUnescapedKorean()
    {
        var item = new VocabItem(
            Id: "kd-0001",
            Ko: "물",
            Vi: "nước",
            Pos: PartOfSpeech.DependentNoun,
            Grade: VocabGrade.Beginner,
            Examples: [],
            Reviewed: true,
            Source: new SourceRef("한국어기초사전", "0001", "https://krdict.korean.go.kr/", "CC BY-SA 2.0 KR"));

        var json = JsonSerializer.Serialize(item, ContentJson.Options);

        json.ShouldContain("\"ko\": \"물\"");
        json.ShouldContain("\"vi\": \"nước\"");
        json.ShouldContain("\"pos\": \"dependent_noun\"");
        json.ShouldContain("\"definition_vi\": null");
        JsonSerializer.Deserialize<VocabItem>(json, ContentJson.Options).ShouldBe(item, new VocabItemComparer());
    }

    [Fact]
    public void Serialize_Exercise_WritesTypeDiscriminator()
    {
        Exercise exercise = new TypeAnswerExercise("e1", "Gõ 'xin chào'", ["안녕하세요"]);

        var json = JsonSerializer.Serialize(exercise, ContentJson.Options);

        json.ShouldContain("\"type\": \"type_answer\"");
        json.ShouldContain("\"accepted_answers\"");
    }

    [Fact]
    public void Options_AreReadOnly()
    {
        ContentJson.Options.IsReadOnly.ShouldBeTrue();
    }

    /// <summary>Record chứa IReadOnlyList → Equals so sánh tham chiếu list; so sánh thủ công các trường chính.</summary>
    private sealed class VocabItemComparer : IEqualityComparer<VocabItem>
    {
        public bool Equals(VocabItem? x, VocabItem? y) =>
            x is not null && y is not null
            && x.Id == y.Id && x.Ko == y.Ko && x.Vi == y.Vi && x.Pos == y.Pos && x.Grade == y.Grade
            && x.Reviewed == y.Reviewed && x.Source == y.Source && x.Examples.SequenceEqual(y.Examples);

        public int GetHashCode(VocabItem obj) => obj.Id.GetHashCode(StringComparison.Ordinal);
    }
}
