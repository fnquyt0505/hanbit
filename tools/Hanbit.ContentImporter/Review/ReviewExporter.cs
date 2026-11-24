using Hanbit.ContentImporter.Krdict;
using Hanbit.Domain.Content;
using Hanbit.Domain.Hangul;

namespace Hanbit.ContentImporter.Review;

/// <summary>Một dòng CSV xuất ra để người duyệt.</summary>
public sealed record ReviewRow(string Topic, VocabItem Item);

/// <summary>
/// Chọn từ cần duyệt theo chủ đề tình huống của krdict (theo thứ tự chủ đề đưa vào), bỏ từ đã có bản duyệt,
/// và tạo CSV cho Excel/Google Sheets (T1.7).
/// </summary>
public static class ReviewExporter
{
    public static IReadOnlyList<ReviewRow> Select(
        IReadOnlyList<VocabItem> vocabulary,
        IReadOnlyList<VocabReview> existingReviews,
        IReadOnlyList<KrdictEntry> lmfEntries,
        IReadOnlyList<string> topics,
        int? limit = null)
    {
        var reviewed = existingReviews.Select(r => r.Id).ToHashSet(StringComparer.Ordinal);
        var topicsById = lmfEntries
            .GroupBy(e => $"kd-{e.TargetCode}", StringComparer.Ordinal)
            .ToDictionary(g => g.Key, g => g.SelectMany(e => e.SubjectCategories ?? []).ToHashSet(StringComparer.Ordinal), StringComparer.Ordinal);

        var rows = new List<ReviewRow>();
        var picked = new HashSet<string>(StringComparer.Ordinal);
        foreach (var topic in topics)
        {
            foreach (var item in vocabulary.OrderBy(v => v.Ko, StringComparer.Ordinal))
            {
                if (!reviewed.Contains(item.Id)
                    && topicsById.TryGetValue(item.Id, out var itemTopics)
                    && itemTopics.Contains(topic)
                    && picked.Add(item.Id))
                {
                    rows.Add(new ReviewRow(topic, item));
                }
            }
        }

        return limit is { } max ? rows.Take(max).ToList() : rows;
    }

    public static string ToCsv(IReadOnlyList<ReviewRow> rows)
    {
        var lines = new List<IReadOnlyList<string?>> { ReviewCsvColumns.All.ToList() };
        lines.AddRange(rows.Select(row => (IReadOnlyList<string?>)
        [
            row.Item.Id,
            row.Topic,
            row.Item.Ko,
            RomanizationService.Romanize(row.Item.Ko),
            VietnamesePartOfSpeech(row.Item.Pos),
            row.Item.Hanja,
            row.Item.Vi,
            row.Item.DefinitionVi,
            string.Empty, // nghia_moi
            string.Empty, // dinh_nghia_moi
            string.Empty, // duyet
            string.Empty, // ghi_chu
            row.Item.Source.Url,
        ]));
        return Csv.Write(lines);
    }

    public static string VietnamesePartOfSpeech(PartOfSpeech pos) => PartOfSpeechNames.Vietnamese(pos);
}
