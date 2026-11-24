using System.Text.Json.Serialization;

using Hanbit.Application.Lessons;
using Hanbit.Domain.Content;
using Hanbit.Domain.Hangul;
using Hanbit.Domain.Learning;

namespace Hanbit.Web.Models.Learn;

/// <summary>Bài học gửi xuống trình duyệt — KHÔNG chứa đáp án (ARCHITECTURE §4).</summary>
public sealed record LessonDto(
    string Course,
    string Slug,
    string Title,
    int Order,
    string? NextSlug,
    IReadOnlyList<VocabDto> Vocab,
    IReadOnlyList<GrammarDto> Grammar,
    IReadOnlyList<ExerciseDto> Exercises)
{
    public static LessonDto From(LessonContent content) => new(
        content.Course.Slug,
        content.Lesson.Slug,
        content.Lesson.Title,
        content.Lesson.Order,
        content.Next?.Slug,
        content.Vocab.Select(VocabDto.From).ToList(),
        content.Grammar.Select(GrammarDto.From).ToList(),
        content.Exercises.Select(ExerciseDto.From).ToList());
}

/// <param name="Romanization">Revised Romanization (tính từ chữ Hàn nếu dữ liệu chưa có).</param>
/// <param name="SourceUrl">Link mục từ gốc trên 한국어기초사전 (ghi công CC BY-SA).</param>
public sealed record VocabDto(
    string Id,
    string Ko,
    string Romanization,
    string Vi,
    string Pos,
    string? Pronunciation,
    string? Hanja,
    IReadOnlyList<Example> Examples,
    string SourceUrl)
{
    public static VocabDto From(VocabItem v) => new(
        v.Id,
        v.Ko,
        v.Romanization ?? RomanizationService.Romanize(v.Ko),
        v.Vi,
        PartOfSpeechNames.Vietnamese(v.Pos),
        v.Pronunciation,
        v.Hanja,
        v.Examples,
        v.Source.Url);
}

public sealed record GrammarDto(string Id, string Pattern, string MeaningVi, string ExplanationVi, IReadOnlyList<Example> Examples)
{
    public static GrammarDto From(GrammarPoint g) => new(g.Id, g.Pattern, g.MeaningVi, g.ExplanationVi, g.Examples);
}

/// <summary>
/// Bài tập không đáp án. Theo <see cref="Type"/>: <c>multiple_choice</c> (prompt, choices) · <c>listen_choose</c> (audioKo, choices)
/// · <c>match_pairs</c> (prompt, left, right — mỗi cột xáo riêng) · <c>reorder</c> (prompt, tokens — đã xáo) · <c>type_answer</c> (prompt).
/// Thứ tự <see cref="Choices"/> giữ nguyên bản gốc (câu trả lời gửi lại vị trí trong danh sách này).
/// </summary>
public sealed record ExerciseDto(
    string Id,
    string Type,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Prompt = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? AudioKo = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Choices = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Left = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Right = null,
    [property: JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] IReadOnlyList<string>? Tokens = null)
{
    public static ExerciseDto From(Exercise exercise)
    {
        var random = new Random(ExerciseGenerator.StableSeed(exercise.Id));
        return exercise switch
        {
            MultipleChoiceExercise mc => new(mc.Id, "multiple_choice", Prompt: mc.Prompt, Choices: mc.Choices),
            ListenChooseExercise lc => new(lc.Id, "listen_choose", AudioKo: lc.AudioKo, Choices: lc.Choices),
            MatchPairsExercise mp => new(mp.Id, "match_pairs", Prompt: mp.Prompt,
                Left: Shuffle(mp.Pairs.Select(p => p.Left), random),
                Right: Shuffle(mp.Pairs.Select(p => p.Right), random)),
            ReorderExercise ro => new(ro.Id, "reorder", Prompt: ro.Prompt, Tokens: ShuffleNotIdentical(ro.Tokens, random)),
            TypeAnswerExercise ta => new(ta.Id, "type_answer", Prompt: ta.Prompt),
            _ => throw new NotSupportedException($"Loại bài tập chưa hỗ trợ: {exercise.GetType().Name}"),
        };
    }

    private static List<string> Shuffle(IEnumerable<string> items, Random random)
    {
        var list = items.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }

    /// <summary>Xáo đến khi khác thứ tự đúng (nếu các mảnh không giống hệt nhau) — tránh lộ đáp án.</summary>
    private static List<string> ShuffleNotIdentical(IReadOnlyList<string> tokens, Random random)
    {
        if (tokens.Distinct(StringComparer.Ordinal).Count() < 2)
        {
            return [.. tokens];
        }

        List<string> shuffled;
        do
        {
            shuffled = Shuffle(tokens, random);
        }
        while (shuffled.SequenceEqual(tokens, StringComparer.Ordinal));

        return shuffled;
    }
}

/// <summary>Yêu cầu chấm: điền đúng một trường của <see cref="Answer"/> theo loại bài tập.</summary>
public sealed record CheckExerciseRequest(string? Course, string? Lesson, string? ExerciseId, AnswerDto? Answer);

/// <param name="Index">multiple_choice, listen_choose: vị trí trong <c>choices</c>.</param>
/// <param name="Pairs">match_pairs: các cặp đã nối.</param>
/// <param name="Tokens">reorder: các mảnh theo thứ tự đã chọn.</param>
/// <param name="Text">type_answer: chữ đã gõ.</param>
public sealed record AnswerDto(int? Index, IReadOnlyList<MatchPair>? Pairs, IReadOnlyList<string>? Tokens, string? Text)
{
    public ExerciseAnswer? ToDomain() => this switch
    {
        { Index: { } index } => new ChoiceAnswer(index),
        { Pairs: { } pairs } => new PairsAnswer(pairs),
        { Tokens: { } tokens } => new OrderAnswer(tokens),
        { Text: { } text } => new TextAnswer(text),
        _ => null,
    };
}

/// <param name="VocabId">Từ vựng mà bài tập kiểm tra (bài sinh từ một từ) — cho danh sách "từ cần ôn lại"; null nếu không gắn với một từ.</param>
public sealed record CheckExerciseResponse(bool Correct, string CorrectAnswer, string? ExplanationVi, string? VocabId = null);
