using Hanbit.Application.Abstractions;
using Hanbit.Domain.Progress;
using Hanbit.Domain.Reminders;

namespace Hanbit.Application.Reminders;

/// <summary>Kết quả một lần chạy: số tài khoản xét / đã nhắc / bỏ qua vì đã học, số thông báo gửi / thiết bị xóa / lỗi.</summary>
public sealed record ReminderRunResult(int Candidates, int Reminded, int SkippedStudied, int Sent, int Removed, int Failed);

/// <summary>
/// Gửi nhắc học (giai đoạn 9 — T9.2). GitHub Actions cron gọi 15 phút/lần qua <c>POST /internal/reminders/run</c>
/// (Render free ngủ khi rảnh nên không tự hẹn giờ trong app được). Tiến độ đọc từ bản trên tài khoản (T8.3).
/// </summary>
public sealed class ReminderJob(IReminderStore reminders, IProgressStore progress, IPushSender sender, IClock clock)
{
    public async Task<ReminderRunResult> RunAsync(CancellationToken cancellationToken)
    {
        var now = clock.UtcNow;
        var today = VietnamTime.ToDate(now);
        var time = VietnamTime.ToTime(now);
        var candidates = await reminders.GetCandidatesAsync(time.Add(-ReminderPolicy.LateWindow), time, today, cancellationToken);

        int reminded = 0, skipped = 0, sent = 0, removed = 0, failed = 0;
        foreach (var candidate in candidates)
        {
            var learner = await progress.GetAsync(candidate.UserId, cancellationToken) ?? LearnerProgress.Empty(now);
            if (!ReminderPolicy.ShouldRemind(candidate.Settings, learner.Activity, now))
            {
                skipped++;
                continue;
            }

            if (!await reminders.TryClaimAsync(candidate.UserId, today, cancellationToken))
            {
                continue; // lần chạy khác đang/đã nhắc tài khoản này
            }

            var summary = ProgressSummaries.Summarize(learner.Activity, learner.Cards.Values, learner.Lessons, learner.TotalXp, now, today);
            var message = ReminderPolicy.BuildMessage(summary.Streak.Current, summary.DueCards);
            var delivered = 0;
            foreach (var device in await reminders.GetDevicesAsync(candidate.UserId, cancellationToken))
            {
                switch (await sender.SendAsync(device, message, cancellationToken))
                {
                    case PushSendStatus.Sent:
                        delivered++;
                        break;
                    case PushSendStatus.Gone:
                        await reminders.RemoveExpiredDeviceAsync(device.Endpoint, cancellationToken);
                        removed++;
                        break;
                    default:
                        failed++;
                        break;
                }
            }

            if (delivered > 0)
            {
                reminded++;
                sent += delivered;
            }
            else
            {
                await reminders.ReleaseClaimAsync(candidate.UserId, today, candidate.Settings.LastSentOn, cancellationToken);
            }
        }

        return new ReminderRunResult(candidates.Count, reminded, skipped, sent, removed, failed);
    }
}
