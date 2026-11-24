using Hanbit.Domain.Progress;
using Hanbit.Web.Models.Learn;

namespace Hanbit.Web.Models.Progress;

/// <summary>Một câu trả lời trong yêu cầu hoàn thành bài.</summary>
public sealed record SubmittedAnswerDto(string? ExerciseId, AnswerDto? Answer);

/// <summary>
/// <c>POST /api/lessons/{c}/{l}/complete</c>: câu trả lời của mọi bài tập + phần tiến độ liên quan từ localStorage.
/// "Hôm nay" do server xác định — không nhận ngày từ trình duyệt.
/// </summary>
/// <param name="Previous">Bản ghi bài này trước đó (để giữ điểm cao nhất).</param>
/// <param name="Cards">Thẻ ôn hiện có (server chỉ dùng thẻ của từ trong bài).</param>
/// <param name="Activity">Hoạt động theo ngày hiện có.</param>
public sealed record CompleteLessonRequest(
    IReadOnlyList<SubmittedAnswerDto>? Answers,
    LessonRecord? Previous,
    IReadOnlyList<ReviewCard>? Cards,
    IReadOnlyList<DailyActivity>? Activity)
{
    public const int MaxAnswers = 200;
    public const int MaxCards = 20_000;
    public const int MaxActivity = LearnerProgress.ActivityHistoryDays + 31;
}

/// <param name="NewCards">Thẻ cần thêm/ghi đè vào <c>cards</c> (key = itemId).</param>
/// <param name="Activity">Danh sách hoạt động mới — thay thế danh sách cũ.</param>
/// <param name="Xp">XP nhận được lần này — cộng vào <c>totalXp</c>.</param>
/// <param name="Today">Ngày hiện tại theo giờ Việt Nam (server).</param>
public sealed record CompleteLessonResponse(
    string LessonKey,
    LessonRecord LessonRecord,
    IReadOnlyList<ReviewCard> NewCards,
    IReadOnlyList<DailyActivity> Activity,
    int Xp,
    Streak Streak,
    int Correct,
    int Total,
    int Score,
    DateOnly Today);
