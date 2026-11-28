namespace Hanbit.Domain.Progress;

/// <summary>
/// Gộp tiến độ của một thiết bị (localStorage) vào bản trên tài khoản (T8.3). Hàm thuần, gửi lại cùng dữ liệu → cùng kết quả
/// (trừ XP, xem <paramref name="baseXp"/> ở <see cref="Merge"/>).
/// <list type="bullet">
/// <item>Bài học: bản "tốt hơn" — đã hoàn thành &gt; đang học; điểm cao nhất; ngày hoàn thành sớm nhất.</item>
/// <item>Thẻ ôn: bản ôn gần nhất (<see cref="ReviewCard.LastReviewedAt"/>); bằng nhau → nhiều lần lặp hơn, rồi hạn ôn muộn hơn.</item>
/// <item>Hoạt động: theo ngày, lấy max XP và max lượt ôn (không cộng — tránh đếm hai lần khi gửi lại); giữ 365 ngày gần nhất.</item>
/// <item>XP: bản server + phần XP thiết bị kiếm thêm từ lần đồng bộ trước (<c>local − baseXp</c>, không âm).</item>
/// </list>
/// </summary>
public static class ProgressMerger
{
    /// <param name="server">Bản trên tài khoản (null = chưa có).</param>
    /// <param name="local">Bản trên thiết bị (đã làm sạch).</param>
    /// <param name="baseXp">
    /// <see cref="LearnerProgress.TotalXp"/> của thiết bị ngay sau lần đồng bộ trước; dữ liệu khách chưa từng đồng bộ → 0
    /// (toàn bộ XP trên máy là XP mới).
    /// </param>
    public static LearnerProgress Merge(LearnerProgress? server, LearnerProgress local, int baseXp)
    {
        if (server is null)
        {
            return local;
        }

        var lessons = new Dictionary<string, LessonRecord>(server.Lessons, StringComparer.Ordinal);
        foreach (var (key, record) in local.Lessons)
        {
            lessons[key] = lessons.TryGetValue(key, out var existing) ? Better(existing, record) : record;
        }

        var cards = new Dictionary<string, ReviewCard>(server.Cards, StringComparer.Ordinal);
        foreach (var (key, card) in local.Cards)
        {
            cards[key] = cards.TryGetValue(key, out var existing) ? Newer(existing, card) : card;
        }

        var activity = server.Activity.Concat(local.Activity)
            .GroupBy(a => a.Date)
            .Select(g => new DailyActivity(g.Key, g.Max(a => a.Xp), g.Max(a => a.Reviews)))
            .OrderBy(a => a.Date)
            .TakeLast(LearnerProgress.ActivityHistoryDays)
            .ToList();

        var earned = Math.Max(0, local.TotalXp - Math.Max(0, baseXp));
        var totalXp = (int)Math.Min(int.MaxValue, (long)server.TotalXp + earned);

        return new LearnerProgress(
            LearnerProgress.CurrentSchemaVersion,
            server.CreatedAt <= local.CreatedAt ? server.CreatedAt : local.CreatedAt,
            lessons,
            cards,
            activity,
            totalXp);
    }

    private static LessonRecord Better(LessonRecord a, LessonRecord b)
    {
        var status = a.Status == LessonStatus.Completed || b.Status == LessonStatus.Completed ? LessonStatus.Completed : LessonStatus.InProgress;
        DateTimeOffset? completedAt = (a.CompletedAt, b.CompletedAt) switch
        {
            ({ } x, { } y) => x <= y ? x : y,
            (var x, var y) => x ?? y,
        };
        return new LessonRecord(status, Math.Max(a.BestScore, b.BestScore), completedAt);
    }

    private static ReviewCard Newer(ReviewCard a, ReviewCard b)
    {
        var byReview = Nullable.Compare(a.LastReviewedAt, b.LastReviewedAt);
        if (byReview != 0)
        {
            return byReview > 0 ? a : b;
        }

        if (a.Repetitions != b.Repetitions)
        {
            return a.Repetitions > b.Repetitions ? a : b;
        }

        return a.DueAt >= b.DueAt ? a : b;
    }
}
