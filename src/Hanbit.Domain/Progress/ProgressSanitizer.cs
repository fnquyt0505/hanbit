using System.Text.RegularExpressions;

namespace Hanbit.Domain.Progress;

/// <summary>Kết quả làm sạch tiến độ nhập từ file.</summary>
/// <param name="Warnings">Các chỉnh sửa đã áp dụng (tiếng Việt, hiển thị cho người dùng).</param>
public sealed record SanitizedProgress(LearnerProgress Progress, IReadOnlyList<string> Warnings);

/// <summary>
/// Làm sạch tiến độ do client gửi (file sao lưu có thể bị sửa tay — ARCHITECTURE §6): kẹp giá trị về giới hạn hợp lệ
/// thay vì từ chối cả file. Không chống gian lận (MVP không có bảng xếp hạng).
/// </summary>
public static partial class ProgressSanitizer
{
    public const int MaxCards = 20_000;
    public const int MaxLessons = 2_000;
    public const int MaxRepetitions = 1_000;

    [GeneratedRegex("^[a-z0-9]+(-[a-z0-9]+)*/[a-z0-9]+(-[a-z0-9]+)*$")]
    private static partial Regex LessonKeyRegex();

    public static SanitizedProgress Sanitize(LearnerProgress input, DateTimeOffset now, DateOnly today)
    {
        var warnings = new List<string>();
        void Warn(string message)
        {
            if (!warnings.Contains(message))
            {
                warnings.Add(message);
            }
        }

        var createdAt = input.CreatedAt == default || input.CreatedAt > now ? now : input.CreatedAt;
        if (createdAt != input.CreatedAt)
        {
            Warn("Ngày tạo không hợp lệ → đặt lại thành hôm nay.");
        }

        // Bài học
        var lessons = new Dictionary<string, LessonRecord>(StringComparer.Ordinal);
        foreach (var (key, record) in input.Lessons ?? new Dictionary<string, LessonRecord>())
        {
            if (record is null || !LessonKeyRegex().IsMatch(key ?? string.Empty) || !Enum.IsDefined(record.Status) || lessons.Count >= MaxLessons)
            {
                Warn("Bỏ qua bản ghi bài học không hợp lệ.");
                continue;
            }

            var bestScore = Math.Clamp(record.BestScore, 0, 100);
            var completedAt = record.CompletedAt > now ? now : record.CompletedAt;
            if (bestScore != record.BestScore || completedAt != record.CompletedAt)
            {
                Warn("Điểm hoặc ngày hoàn thành bài học vượt giới hạn → đã điều chỉnh.");
            }

            lessons[key!] = record with { BestScore = bestScore, CompletedAt = completedAt };
        }

        // Thẻ ôn tập — key luôn = itemId
        var cards = new Dictionary<string, ReviewCard>(StringComparer.Ordinal);
        foreach (var card in (input.Cards ?? new Dictionary<string, ReviewCard>()).Values)
        {
            if (card is null || string.IsNullOrWhiteSpace(card.ItemId) || card.ItemId.Length > 64 || !Enum.IsDefined(card.ItemType)
                || card.DueAt == default || cards.ContainsKey(card.ItemId) || cards.Count >= MaxCards)
            {
                Warn("Bỏ qua thẻ ôn tập không hợp lệ hoặc bị trùng.");
                continue;
            }

            var ease = double.IsFinite(card.Ease) ? Math.Clamp(card.Ease, ReviewCard.MinEase, ReviewCard.MaxEase) : ReviewCard.DefaultEase;
            var interval = Math.Clamp(card.IntervalDays, 0, ReviewCard.MaxIntervalDays);
            var repetitions = Math.Clamp(card.Repetitions, 0, MaxRepetitions);
            var lastReviewed = card.LastReviewedAt > now ? now : card.LastReviewedAt;
            var fixedCard = card with { Ease = ease, IntervalDays = interval, Repetitions = repetitions, LastReviewedAt = lastReviewed };
            if (fixedCard != card)
            {
                Warn("Thông số thẻ ôn tập vượt giới hạn (độ dễ 1,3–5, khoảng ôn ≤ 3650 ngày) → đã điều chỉnh.");
            }

            cards[card.ItemId] = fixedCard;
        }

        // Hoạt động: gộp trùng ngày, bỏ ngày tương lai, giữ 365 ngày, giá trị ≥ 0
        var raw = input.Activity ?? [];
        var activity = raw
            .Where(a => a is not null && a.Date <= today && a.Date > today.AddDays(-LearnerProgress.ActivityHistoryDays))
            .GroupBy(a => a.Date)
            .Select(g => new DailyActivity(g.Key, g.Sum(a => Math.Max(0, a.Xp)), g.Sum(a => Math.Max(0, a.Reviews))))
            .OrderBy(a => a.Date)
            .ToList();
        if (activity.Count != raw.Count || raw.Any(a => a is not null && (a.Xp < 0 || a.Reviews < 0)))
        {
            Warn("Lịch hoạt động có ngày trùng, ngày tương lai, quá 365 ngày hoặc giá trị âm → đã điều chỉnh.");
        }

        var totalXp = Math.Max(0, input.TotalXp);
        if (totalXp != input.TotalXp)
        {
            Warn("Tổng XP âm → đặt về 0.");
        }

        return new SanitizedProgress(
            new LearnerProgress(LearnerProgress.CurrentSchemaVersion, createdAt, lessons, cards, activity, totalXp),
            warnings);
    }
}
