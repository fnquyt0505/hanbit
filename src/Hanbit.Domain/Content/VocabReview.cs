namespace Hanbit.Domain.Content;

/// <summary>
/// Một bản duyệt/sửa nghĩa tiếng Việt cho từ vựng krdict (content/vocab-reviews.json).
/// File krdict-*.json chỉ sinh bởi ContentImporter (không sửa tay) → mọi chỉnh sửa của người duyệt nằm ở đây.
/// </summary>
/// <param name="Id">Id từ vựng, VD <c>kd-12345</c>.</param>
/// <param name="Reviewed">Người duyệt xác nhận nghĩa dùng được → từ được phép vào bài học.</param>
/// <param name="Vi">Nghĩa tiếng Việt thay thế (null = giữ nghĩa từ krdict).</param>
/// <param name="DefinitionVi">Định nghĩa tiếng Việt thay thế (null = giữ nguyên).</param>
/// <param name="Note">Ghi chú nội bộ của người duyệt (không hiển thị cho người học).</param>
public sealed record VocabReview(
    string Id,
    bool Reviewed,
    string? Vi = null,
    string? DefinitionVi = null,
    string? Note = null);

/// <summary>Gộp bản duyệt vào từ vựng krdict (hàm thuần, dùng chung cho repository và validator).</summary>
public static class VocabReviewMerger
{
    /// <summary>
    /// Áp dụng <paramref name="reviews"/> lên <paramref name="items"/>: <c>reviewed</c> lấy theo bản duyệt,
    /// <c>vi</c>/<c>definition_vi</c> được thay nếu bản duyệt có giá trị. Từ không có bản duyệt giữ nguyên.
    /// Bản duyệt trỏ tới id không tồn tại bị bỏ qua (validator báo lỗi riêng).
    /// </summary>
    public static IReadOnlyList<VocabItem> Apply(IEnumerable<VocabItem> items, IEnumerable<VocabReview> reviews)
    {
        var reviewById = new Dictionary<string, VocabReview>(StringComparer.Ordinal);
        foreach (var review in reviews)
        {
            reviewById[review.Id] = review; // trùng id: bản sau thắng (validator báo trùng)
        }

        return items
            .Select(item => reviewById.TryGetValue(item.Id, out var review)
                ? item with
                {
                    Reviewed = review.Reviewed,
                    Vi = review.Vi ?? item.Vi,
                    DefinitionVi = review.DefinitionVi ?? item.DefinitionVi,
                }
                : item)
            .ToList();
    }
}
