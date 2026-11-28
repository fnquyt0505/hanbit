using Hanbit.Domain.Progress;
using Hanbit.Domain.Reminders;

using Shouldly;

namespace Hanbit.Domain.Tests.Reminders;

/// <summary>T9.2: nhắc khi đã tới giờ (giờ VN), hôm nay chưa nhắc và chưa học.</summary>
public class ReminderPolicyTests
{
    private static readonly DateOnly Today = new(2026, 11, 28);

    /// <summary>Thời điểm UTC ứng với <paramref name="hour"/>:<paramref name="minute"/> giờ VN ngày 28/11.</summary>
    private static DateTimeOffset Vn(int hour, int minute = 0) => new DateTimeOffset(2026, 11, 28, hour, minute, 0, TimeSpan.FromHours(7)).ToUniversalTime();

    private static readonly ReminderSettings At20 = new(true, new TimeOnly(20, 0), null);

    [Fact]
    public void VietnamTime_ToTime_ConvertsFromUtc()
    {
        VietnamTime.ToTime(new DateTimeOffset(2026, 11, 28, 13, 5, 0, TimeSpan.Zero)).ShouldBe(new TimeOnly(20, 5));
    }

    [Theory]
    [InlineData(19, 59, false)] // chưa tới giờ
    [InlineData(20, 0, true)]
    [InlineData(20, 14, true)] // cron trễ vài phút
    [InlineData(21, 59, true)]
    [InlineData(22, 0, false)] // trễ ≥ 2 giờ (lịch bị gián đoạn) → bỏ
    public void ShouldRemind_TimeWindow(int hour, int minute, bool expected)
    {
        ReminderPolicy.ShouldRemind(At20, [], Vn(hour, minute)).ShouldBe(expected);
    }

    [Fact]
    public void ShouldRemind_No_WhenDisabled_AlreadySentToday_OrStudiedToday()
    {
        ReminderPolicy.ShouldRemind(At20 with { Enabled = false }, [], Vn(20, 5)).ShouldBeFalse();
        ReminderPolicy.ShouldRemind(At20 with { LastSentOn = Today }, [], Vn(20, 5)).ShouldBeFalse();
        ReminderPolicy.ShouldRemind(At20, [new DailyActivity(Today, 10, 0)], Vn(20, 5)).ShouldBeFalse();
    }

    [Fact]
    public void ShouldRemind_Yes_WhenSentYesterday_AndOnlyStudiedYesterday()
    {
        var settings = At20 with { LastSentOn = Today.AddDays(-1) };

        ReminderPolicy.ShouldRemind(settings, [new DailyActivity(Today.AddDays(-1), 20, 3)], Vn(20, 5)).ShouldBeTrue();
    }

    [Fact]
    public void ShouldRemind_ReviewsWithoutXp_DoNotCountAsStudied()
    {
        // "Ngày có học" = có ít nhất 1 XP (cùng định nghĩa streak)
        ReminderPolicy.ShouldRemind(At20, [new DailyActivity(Today, 0, 0)], Vn(20, 5)).ShouldBeTrue();
    }

    [Fact]
    public void BuildMessage_WithStreakAndDueCards_LinksToReview()
    {
        var message = ReminderPolicy.BuildMessage(currentStreak: 5, dueCards: 12);

        message.Title.ShouldBe("🔥 Giữ chuỗi 5 ngày nhé!");
        message.Body.ShouldBe("Hôm nay bạn chưa học — 12 thẻ đang chờ ôn. Chỉ cần 5 phút thôi!");
        message.Url.ShouldBe("/review");
        message.Tag.ShouldBe("hanbit-reminder");
    }

    [Fact]
    public void BuildMessage_NoStreakNoCards_LinksToDashboard()
    {
        var message = ReminderPolicy.BuildMessage(0, 0);

        message.Title.ShouldBe("Đến giờ học tiếng Hàn rồi! 📚");
        message.Body.ShouldBe("Hôm nay bạn chưa học. Chỉ cần 5 phút thôi!");
        message.Url.ShouldBe("/dashboard");
    }
}
