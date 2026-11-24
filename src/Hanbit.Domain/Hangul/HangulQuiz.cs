using Hanbit.Domain.Content;

namespace Hanbit.Domain.Hangul;

/// <summary>Dạng câu hỏi quiz Hangul.</summary>
public enum HangulQuizKind
{
    /// <summary>Nhìn chữ cái → chọn phiên âm.</summary>
    CharToRomanization,

    /// <summary>Nhìn phiên âm → chọn chữ cái.</summary>
    RomanizationToChar,

    /// <summary>Nghe → chọn chữ cái (prompt là chữ được đọc, xem <see cref="HangulPronunciation"/>).</summary>
    ListenToChar,
}

/// <summary>Câu hỏi quiz (CÓ đáp án — chỉ dùng ở server).</summary>
public sealed record HangulQuizQuestion(HangulQuizKind Kind, string Prompt, IReadOnlyList<string> Choices, string Answer);

/// <summary>Kết quả chấm một câu.</summary>
public sealed record HangulQuizCheck(bool Correct, string CorrectAnswer, string LetterChar, string Romanization);

/// <summary>Sinh và chấm quiz nhận diện chữ cái cho một nhóm (logic thuần, ngẫu nhiên theo seed).</summary>
public static class HangulQuiz
{
    public const int DefaultQuestionCount = 10;
    public const int ChoiceCount = 4;

    private static readonly HangulQuizKind[] Kinds = Enum.GetValues<HangulQuizKind>();

    /// <summary>
    /// Sinh <paramref name="count"/> câu từ các chữ của nhóm: mỗi chữ xuất hiện trước khi một chữ bị lặp lại,
    /// dạng câu xoay vòng ngẫu nhiên, mỗi câu tối đa 4 lựa chọn khác nhau lấy trong nhóm.
    /// </summary>
    public static IReadOnlyList<HangulQuizQuestion> Generate(IReadOnlyList<HangulLetter> groupLetters, int seed, int count = DefaultQuestionCount)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(groupLetters.Count, 2, nameof(groupLetters));
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(count);

        var random = new Random(seed);
        var questions = new List<HangulQuizQuestion>(count);
        var queue = new Queue<HangulLetter>();
        var kindOffset = random.Next(Kinds.Length);

        for (var i = 0; i < count; i++)
        {
            if (queue.Count == 0)
            {
                foreach (var letter in Shuffle(groupLetters, random))
                {
                    queue.Enqueue(letter);
                }
            }

            var target = queue.Dequeue();
            var kind = Kinds[(i + kindOffset) % Kinds.Length];
            var distractors = Shuffle(groupLetters.Where(l => l.Id != target.Id).ToList(), random).Take(ChoiceCount - 1);
            var options = Shuffle(distractors.Append(target).ToList(), random);

            questions.Add(kind switch
            {
                HangulQuizKind.CharToRomanization =>
                    new(kind, target.Char, options.Select(l => l.Romanization).ToList(), target.Romanization),
                HangulQuizKind.RomanizationToChar =>
                    new(kind, target.Romanization, options.Select(l => l.Char).ToList(), target.Char),
                _ =>
                    new(kind, HangulPronunciation.SpeakText(target), options.Select(l => l.Char).ToList(), target.Char),
            });
        }

        return questions;
    }

    /// <summary>
    /// Chấm câu trả lời (stateless): tìm chữ cái theo <paramref name="prompt"/> và dạng câu trong nhóm.
    /// Không tìm thấy chữ → null (yêu cầu không hợp lệ).
    /// </summary>
    public static HangulQuizCheck? Check(IReadOnlyList<HangulLetter> groupLetters, HangulQuizKind kind, string prompt, string answer)
    {
        var letter = kind switch
        {
            HangulQuizKind.CharToRomanization => groupLetters.FirstOrDefault(l => l.Char == prompt),
            HangulQuizKind.RomanizationToChar => groupLetters.FirstOrDefault(l => l.Romanization == prompt),
            _ => groupLetters.FirstOrDefault(l => HangulPronunciation.SpeakText(l) == prompt),
        };

        if (letter is null)
        {
            return null;
        }

        var correctAnswer = kind == HangulQuizKind.CharToRomanization ? letter.Romanization : letter.Char;
        return new HangulQuizCheck(
            string.Equals(answer?.Trim(), correctAnswer, StringComparison.Ordinal),
            correctAnswer,
            letter.Char,
            letter.Romanization);
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
