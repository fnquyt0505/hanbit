using Hanbit.Domain.Progress;

namespace Hanbit.Application.Abstractions;

/// <summary>Đồng hồ của server — "hôm nay" theo giờ Việt Nam, không tin ngày của trình duyệt (ARCHITECTURE §4).</summary>
public interface IClock
{
    DateTimeOffset UtcNow { get; }

    /// <summary>Ngày hiện tại theo Asia/Ho_Chi_Minh.</summary>
    DateOnly Today => VietnamTime.ToDate(UtcNow);
}
