using Hanbit.Application.Abstractions;
using Hanbit.Domain.Learning;
using Hanbit.Domain.Progress;

namespace Hanbit.Application.Lessons;

/// <summary>Câu trả lời một bài tập gửi khi hoàn thành bài.</summary>
public sealed record SubmittedAnswer(string ExerciseId, ExerciseAnswer Answer);

/// <summary>Yêu cầu hoàn thành bài: câu trả lời + phần tiến độ liên quan (client giữ toàn bộ tiến độ).</summary>
public sealed record SubmitLessonCommand(
    string Course,
    string Lesson,
    IReadOnlyList<SubmittedAnswer> Answers,
    LessonRecord? Previous,
    IReadOnlyList<ReviewCard> Cards,
    IReadOnlyList<DailyActivity> Activity);

/// <summary>
/// Hoàn thành bài học (stateless): CHẤM LẠI mọi câu trả lời ở server (không tin điểm client gửi), bài tập không có câu
/// trả lời tính là sai, rồi áp dụng <see cref="LessonCompletion"/> với "hôm nay" theo <see cref="IClock"/>.
/// </summary>
public sealed class SubmitLessonHandler(LessonService lessonService, IClock clock)
{
    /// <returns>null nếu bài học không tồn tại.</returns>
    public async Task<LessonCompletionResult?> HandleAsync(SubmitLessonCommand command, CancellationToken cancellationToken)
    {
        var content = await lessonService.GetAsync(command.Course, command.Lesson, cancellationToken);
        if (content is null)
        {
            return null;
        }

        var answers = command.Answers
            .GroupBy(a => a.ExerciseId, StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.Last().Answer, StringComparer.Ordinal);

        var correct = 0;
        var wrongVocab = new HashSet<string>(StringComparer.Ordinal);
        foreach (var exercise in content.Exercises)
        {
            var isCorrect = answers.TryGetValue(exercise.Id, out var answer) && AnswerChecker.Check(exercise, answer)?.Correct == true;
            if (isCorrect)
            {
                correct++;
            }
            else if (ExerciseGenerator.VocabIdFor(exercise.Id) is { } vocabId)
            {
                wrongVocab.Add(vocabId);
            }
        }

        return LessonCompletion.Complete(
            command.Previous,
            correct,
            content.Exercises.Count,
            content.Vocab.Select(v => v.Id).ToList(),
            wrongVocab,
            command.Cards,
            command.Activity,
            clock.UtcNow,
            clock.Today);
    }
}
