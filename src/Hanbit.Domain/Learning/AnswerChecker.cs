using Hanbit.Domain.Content;

namespace Hanbit.Domain.Learning;

/// <summary>Câu trả lời của người học — loại phải khớp loại bài tập.</summary>
public abstract record ExerciseAnswer;

/// <summary>Chọn đáp án (multiple_choice, listen_choose): vị trí trong danh sách lựa chọn GỐC.</summary>
public sealed record ChoiceAnswer(int Index) : ExerciseAnswer;

/// <summary>Nối cặp: các cặp trái–phải người học đã nối.</summary>
public sealed record PairsAnswer(IReadOnlyList<MatchPair> Pairs) : ExerciseAnswer;

/// <summary>Sắp xếp: các mảnh theo thứ tự người học chọn.</summary>
public sealed record OrderAnswer(IReadOnlyList<string> Tokens) : ExerciseAnswer;

/// <summary>Gõ câu trả lời.</summary>
public sealed record TextAnswer(string Text) : ExerciseAnswer;

/// <summary>Kết quả chấm.</summary>
/// <param name="CorrectAnswer">Đáp án đúng dạng hiển thị (VD "감사합니다", "가족 — gia đình; …").</param>
public sealed record CheckResult(bool Correct, string CorrectAnswer, string? ExplanationVi);

/// <summary>Chấm bài tập (logic thuần). So sánh chữ qua <see cref="TextNormalizer"/>.</summary>
public static class AnswerChecker
{
    /// <summary>Chấm; loại câu trả lời không khớp loại bài tập → null (yêu cầu không hợp lệ).</summary>
    public static CheckResult? Check(Exercise exercise, ExerciseAnswer answer) => (exercise, answer) switch
    {
        (MultipleChoiceExercise mc, ChoiceAnswer a) => Result(a.Index == mc.AnswerIndex, mc.Choices[mc.AnswerIndex], mc),
        (ListenChooseExercise lc, ChoiceAnswer a) => Result(a.Index == lc.AnswerIndex, lc.Choices[lc.AnswerIndex], lc),
        (MatchPairsExercise mp, PairsAnswer a) => Result(PairsMatch(mp.Pairs, a.Pairs), FormatPairs(mp.Pairs), mp),
        (ReorderExercise ro, OrderAnswer a) => Result(OrderMatches(ro.Tokens, a.Tokens), string.Join(' ', ro.Tokens), ro),
        (TypeAnswerExercise ta, TextAnswer a) => Result(TextMatches(ta.AcceptedAnswers, a.Text), ta.AcceptedAnswers[0], ta),
        _ => null,
    };

    private static CheckResult Result(bool correct, string correctAnswer, Exercise exercise) =>
        new(correct, correctAnswer, exercise.ExplanationVi);

    private static bool PairsMatch(IReadOnlyList<MatchPair> expected, IReadOnlyList<MatchPair>? actual)
    {
        if (actual is null || actual.Count != expected.Count)
        {
            return false;
        }

        static string Key(MatchPair p) => $"{TextNormalizer.Normalize(p.Left)}\u0001{TextNormalizer.Normalize(p.Right)}";
        return expected.Select(Key).ToHashSet().SetEquals(actual.Select(Key));
    }

    /// <summary>So chuỗi ghép (không so từng mảnh) → hai mảnh giống hệt nhau đổi chỗ vẫn đúng.</summary>
    private static bool OrderMatches(IReadOnlyList<string> expected, IReadOnlyList<string>? actual) =>
        actual is not null
        && actual.Count == expected.Count
        && TextNormalizer.Normalize(string.Join(' ', actual)) == TextNormalizer.Normalize(string.Join(' ', expected));

    private static bool TextMatches(IReadOnlyList<string> accepted, string? text)
    {
        var normalized = TextNormalizer.Normalize(text);
        return normalized.Length > 0 && accepted.Any(a => TextNormalizer.Normalize(a) == normalized);
    }

    private static string FormatPairs(IReadOnlyList<MatchPair> pairs) =>
        string.Join("; ", pairs.Select(p => $"{p.Left} — {p.Right}"));
}
