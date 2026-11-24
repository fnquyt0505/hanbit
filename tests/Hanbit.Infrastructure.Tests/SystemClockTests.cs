using Hanbit.Application.Abstractions;
using Hanbit.Infrastructure;

using Shouldly;

namespace Hanbit.Infrastructure.Tests;

public class SystemClockTests
{
    private sealed class FixedTimeProvider(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    [Fact]
    public void Today_AfterVietnamMidnight_IsNextDay()
    {
        IClock clock = new SystemClock(new FixedTimeProvider(new DateTimeOffset(2026, 11, 24, 17, 30, 0, TimeSpan.Zero)));

        clock.Today.ShouldBe(new DateOnly(2026, 11, 25));
        clock.UtcNow.ShouldBe(new DateTimeOffset(2026, 11, 24, 17, 30, 0, TimeSpan.Zero));
    }
}
