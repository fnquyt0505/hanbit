using System.Text.Json;

using Hanbit.Application.Abstractions;
using Hanbit.Domain.Progress;

namespace Hanbit.Application.Progress;

/// <summary>Cách đồng bộ: gộp với bản trên tài khoản, hoặc thay hẳn (nhập file sao lưu / xóa tiến độ khi đã đăng nhập).</summary>
public enum ProgressSyncMode
{
    Merge,
    Replace,
}

/// <summary>Kết quả đồng bộ: bản trên tài khoản sau khi gộp (thiết bị ghi đè localStorage bằng bản này) hoặc lỗi.</summary>
public sealed record ProgressSyncResult(LearnerProgress? Progress, string? Error);

/// <summary>
/// Đồng bộ tiến độ thiết bị ↔ tài khoản (T8.3). Server vẫn tính mọi thứ; trình duyệt giữ bản làm việc ở localStorage
/// và gửi lên sau mỗi thay đổi. Dữ liệu thiết bị đi qua <see cref="ProgressImporter"/> (migrate + làm sạch) như file sao lưu.
/// </summary>
public sealed class ProgressSyncService(IProgressStore store, ProgressImporter importer)
{
    /// <summary>Bản trên tài khoản (null = chưa có).</summary>
    public Task<LearnerProgress?> GetAsync(string userId, CancellationToken cancellationToken) =>
        store.GetAsync(userId, cancellationToken);

    /// <param name="local">Tiến độ trên thiết bị (JSON như localStorage); null → chỉ lấy bản trên tài khoản.</param>
    /// <param name="baseXp">XP thiết bị lúc đồng bộ lần trước (dữ liệu khách → 0) — xem <see cref="ProgressMerger"/>.</param>
    public async Task<ProgressSyncResult> SyncAsync(
        string userId,
        JsonElement? local,
        int baseXp,
        ProgressSyncMode mode,
        JsonSerializerOptions jsonOptions,
        CancellationToken cancellationToken)
    {
        if (local is not { } element || element.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
        {
            return new ProgressSyncResult(await store.GetAsync(userId, cancellationToken), null);
        }

        var imported = importer.Import(element, jsonOptions);
        if (imported.Progress is not { } device)
        {
            return new ProgressSyncResult(null, imported.Error);
        }

        var saved = await store.UpdateAsync(
            userId,
            server => mode == ProgressSyncMode.Replace ? device : ProgressMerger.Merge(server, device, baseXp),
            cancellationToken);
        return new ProgressSyncResult(saved, null);
    }
}
