using Hanbit.Domain.Progress;

namespace Hanbit.Application.Abstractions;

/// <summary>Tiến độ lưu trên tài khoản (T8.3) — bảng <c>learner_progress</c>, một dòng mỗi tài khoản.</summary>
public interface IProgressStore
{
    Task<LearnerProgress?> GetAsync(string userId, CancellationToken cancellationToken);

    /// <summary>
    /// Đọc bản hiện tại → <paramref name="update"/> → ghi, trong một giao dịch có khóa dòng (hai thiết bị đồng bộ cùng lúc
    /// không ghi đè mất dữ liệu của nhau). Trả về bản đã ghi.
    /// </summary>
    Task<LearnerProgress> UpdateAsync(string userId, Func<LearnerProgress?, LearnerProgress> update, CancellationToken cancellationToken);
}
