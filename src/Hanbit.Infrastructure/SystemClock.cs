using Hanbit.Application.Abstractions;

namespace Hanbit.Infrastructure;

/// <summary>Đồng hồ hệ thống (dùng <see cref="TimeProvider"/> để test thay được).</summary>
public sealed class SystemClock(TimeProvider timeProvider) : IClock
{
    public DateTimeOffset UtcNow => timeProvider.GetUtcNow();
}
