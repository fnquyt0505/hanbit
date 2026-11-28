using Hanbit.Domain.Reminders;

using Shouldly;

namespace Hanbit.Domain.Tests.Reminders;

public class ReminderSettingsTests
{
    [Theory]
    [InlineData(5, 0, true)]
    [InlineData(20, 0, true)]
    [InlineData(20, 30, true)]
    [InlineData(23, 0, true)]
    [InlineData(4, 30, false)]
    [InlineData(23, 30, false)]
    [InlineData(20, 15, false)]
    public void IsValidTime_WindowAndHalfHourSteps(int hour, int minute, bool expected)
    {
        ReminderSettings.IsValidTime(new TimeOnly(hour, minute)).ShouldBe(expected);
    }

    [Fact]
    public void IsValidTime_RejectsSeconds()
    {
        ReminderSettings.IsValidTime(new TimeOnly(20, 0, 1)).ShouldBeFalse();
    }

    [Fact]
    public void AllowedTimes_From0500To2300_Every30Minutes_AllValid()
    {
        var times = ReminderSettings.AllowedTimes;

        times.Count.ShouldBe(37);
        times[0].ShouldBe(new TimeOnly(5, 0));
        times[^1].ShouldBe(new TimeOnly(23, 0));
        times.ShouldAllBe(t => ReminderSettings.IsValidTime(t));
        times.ShouldContain(ReminderSettings.DefaultTime);
    }

    [Fact]
    public void Default_IsOffAt2000()
    {
        ReminderSettings.Default.ShouldBe(new ReminderSettings(false, new TimeOnly(20, 0), null));
    }
}
