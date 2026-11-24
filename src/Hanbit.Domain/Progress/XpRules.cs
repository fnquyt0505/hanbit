namespace Hanbit.Domain.Progress;

/// <summary>
/// Quy tắc XP (người dùng chốt 2026-11-24): theo số câu đúng; học lại bài cũ vẫn được XP.
/// </summary>
public static class XpRules
{
    public const int LessonCompletion = 10;
    public const int HangulQuizCompletion = 5;
    public const int PerCorrectAnswer = 1;
    public const int PerReviewedCard = 1;

    /// <summary>Hoàn thành bài học: 10 + 1 mỗi câu đúng.</summary>
    public static int ForLesson(int correctAnswers) => LessonCompletion + PerCorrectAnswer * Math.Max(0, correctAnswers);

    /// <summary>Hoàn thành quiz Hangul: 5 + 1 mỗi câu đúng.</summary>
    public static int ForHangulQuiz(int correctAnswers) => HangulQuizCompletion + PerCorrectAnswer * Math.Max(0, correctAnswers);

    /// <summary>Ôn thẻ: 1 mỗi thẻ đã đánh giá.</summary>
    public static int ForReviews(int reviewedCards) => PerReviewedCard * Math.Max(0, reviewedCards);
}
