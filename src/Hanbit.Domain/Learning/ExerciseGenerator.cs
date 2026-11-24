using Hanbit.Domain.Content;

namespace Hanbit.Domain.Learning;

/// <summary>
/// Sinh bài tập từ từ vựng của bài học (ARCHITECTURE §5): seed = hash ổn định của "khóa/bài" → cùng bài luôn ra cùng
/// bộ bài tập (server chấm lại được mà không cần lưu trạng thái). Id bài sinh bắt đầu bằng <see cref="IdPrefix"/>.
/// </summary>
public static class ExerciseGenerator
{
    public const string IdPrefix = "gen-";

    private const int ChoiceCount = 4;
    private const int PerKind = 2;
    private const int MaxPairs = 5;

    /// <summary>
    /// Sinh tối đa 7 bài: 2 Hàn→Việt, 2 Việt→Hàn, 2 nghe→nghĩa (lựa chọn), 1 nối cặp. Từ dùng làm đáp án được chọn
    /// ngẫu nhiên theo seed; lựa chọn nhiễu không trùng nghĩa (<see cref="ShortMeaning"/>) với đáp án.
    /// Cần ít nhất 4 từ khác nghĩa; ít hơn → không sinh.
    /// </summary>
    public static IReadOnlyList<Exercise> Generate(string courseSlug, string lessonSlug, IReadOnlyList<VocabItem> vocab)
    {
        var distinct = vocab
            .GroupBy(v => ShortMeaning(v.Vi), StringComparer.Ordinal)
            .Select(g => g.First())
            .DistinctBy(v => v.Ko, StringComparer.Ordinal)
            .ToList();
        if (distinct.Count < ChoiceCount)
        {
            return [];
        }

        var random = new Random(StableSeed($"{courseSlug}/{lessonSlug}"));
        var targets = Shuffle(distinct, random);
        var exercises = new List<Exercise>();
        var next = 0;
        VocabItem Take() => targets[next++ % targets.Count];

        for (var i = 0; i < PerKind; i++)
        {
            var word = Take();
            var (choices, answer) = Choices(word, vocab, random, v => ShortMeaning(v.Vi));
            exercises.Add(new MultipleChoiceExercise($"{IdPrefix}nghia-{word.Id}", $"\"{word.Ko}\" nghĩa là gì?", choices, answer));
        }

        for (var i = 0; i < PerKind; i++)
        {
            var word = Take();
            var (choices, answer) = Choices(word, vocab, random, v => v.Ko);
            exercises.Add(new MultipleChoiceExercise($"{IdPrefix}tu-{word.Id}", $"\"{ShortMeaning(word.Vi)}\" là từ nào?", choices, answer));
        }

        for (var i = 0; i < PerKind; i++)
        {
            var word = Take();
            var (choices, answer) = Choices(word, vocab, random, v => ShortMeaning(v.Vi));
            exercises.Add(new ListenChooseExercise($"{IdPrefix}nghe-{word.Id}", word.Ko, choices, answer));
        }

        var pairs = Shuffle(distinct, random)
            .Take(Math.Min(MaxPairs, distinct.Count))
            .Select(v => new MatchPair(v.Ko, ShortMeaning(v.Vi)))
            .ToList();
        exercises.Add(new MatchPairsExercise($"{IdPrefix}noi-cap", "Nối từ với nghĩa", pairs));

        return exercises;
    }

    /// <summary>Nghĩa ngắn để làm lựa chọn: phần trước dấu ";" đầu tiên (nghĩa thứ nhất).</summary>
    public static string ShortMeaning(string vi)
    {
        var semicolon = vi.IndexOf(';', StringComparison.Ordinal);
        return (semicolon < 0 ? vi : vi[..semicolon]).Trim();
    }

    /// <summary>FNV-1a 32-bit — ổn định giữa các lần chạy (khác <see cref="string.GetHashCode()"/>).</summary>
    public static int StableSeed(string text)
    {
        unchecked
        {
            var hash = 2166136261u;
            foreach (var c in text)
            {
                hash = (hash ^ c) * 16777619u;
            }

            return (int)(hash & 0x7FFFFFFF);
        }
    }

    private static (IReadOnlyList<string> Choices, int AnswerIndex) Choices(
        VocabItem word, IReadOnlyList<VocabItem> vocab, Random random, Func<VocabItem, string> display)
    {
        var answerMeaning = ShortMeaning(word.Vi);
        var distractors = Shuffle(vocab, random)
            .Where(v => v.Id != word.Id && ShortMeaning(v.Vi) != answerMeaning)
            .Select(display)
            .Where(d => d != display(word))
            .Distinct(StringComparer.Ordinal)
            .Take(ChoiceCount - 1);
        var choices = Shuffle(distractors.Append(display(word)), random);
        return (choices, choices.IndexOf(display(word)));
    }

    private static List<T> Shuffle<T>(IEnumerable<T> items, Random random)
    {
        var list = items.ToList();
        for (var i = list.Count - 1; i > 0; i--)
        {
            var j = random.Next(i + 1);
            (list[i], list[j]) = (list[j], list[i]);
        }

        return list;
    }
}
