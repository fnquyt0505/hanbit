using System.Globalization;
using System.Text;

using Hanbit.ContentImporter.Krdict;
using Hanbit.Domain.Content;

namespace Hanbit.ContentImporter.Review;

/// <summary>Kết quả nhập CSV duyệt.</summary>
public sealed record ReviewImportResult(
    IReadOnlyList<VocabReview> Reviews,
    int Approved,
    int Rejected,
    int Undecided,
    int MeaningChanged,
    IReadOnlyList<string> Errors);

/// <summary>
/// Đọc CSV người dùng đã duyệt và GỘP vào danh sách bản duyệt hiện có (vocab-reviews.json): dòng có quyết định
/// ghi đè bản duyệt cũ cùng id, dòng chưa quyết bỏ qua, bản duyệt khác giữ nguyên. Có lỗi → không trả bản duyệt nào
/// (nơi gọi không ghi file — tránh ghi nửa vời).
/// </summary>
public static class ReviewImporter
{
    private static readonly HashSet<string> ApproveValues = new(StringComparer.Ordinal) { "x", "co", "có", "y", "yes", "1", "ok", "✓", "✔", "duyet", "duyệt" };
    private static readonly HashSet<string> RejectValues = new(StringComparer.Ordinal) { "loai", "loại", "khong", "không", "n", "no", "0", "-" };

    public static ReviewImportResult Merge(string csvText, IReadOnlyList<VocabItem> vocabulary, IReadOnlyList<VocabReview> existingReviews)
    {
        var errors = new List<string>();
        var rows = Csv.Parse(csvText);
        if (rows.Count == 0)
        {
            return Failed("File CSV rỗng.");
        }

        var header = rows[0].Select(h => h.Trim().ToLowerInvariant()).ToList();
        int Column(string name) => header.IndexOf(name);
        var idColumn = Column(ReviewCsvColumns.Id);
        var decisionColumn = Column(ReviewCsvColumns.Decision);
        if (idColumn < 0 || decisionColumn < 0)
        {
            return Failed($"Thiếu cột bắt buộc '{ReviewCsvColumns.Id}' hoặc '{ReviewCsvColumns.Decision}' ở dòng tiêu đề.");
        }

        var meaningColumn = Column(ReviewCsvColumns.NewMeaning);
        var definitionColumn = Column(ReviewCsvColumns.NewDefinition);
        var noteColumn = Column(ReviewCsvColumns.Note);
        var vocabIds = vocabulary.Select(v => v.Id).ToHashSet(StringComparer.Ordinal);

        var merged = existingReviews.ToDictionary(r => r.Id, StringComparer.Ordinal);
        var seenInCsv = new HashSet<string>(StringComparer.Ordinal);
        int approved = 0, rejected = 0, undecided = 0, meaningChanged = 0;

        for (var i = 1; i < rows.Count; i++)
        {
            var line = i + 1;
            string Cell(int column) => column >= 0 && column < rows[i].Count ? rows[i][column] : string.Empty;

            var id = Cell(idColumn).Trim();
            if (!vocabIds.Contains(id))
            {
                errors.Add($"Dòng {line}: id '{id}' không có trong từ vựng.");
                continue;
            }

            if (!seenInCsv.Add(id))
            {
                errors.Add($"Dòng {line}: id '{id}' xuất hiện nhiều lần.");
                continue;
            }

            var decision = KrdictVocabMapper.Normalize(Cell(decisionColumn))?.ToLowerInvariant() ?? string.Empty;
            bool reviewed;
            if (decision.Length == 0)
            {
                undecided++;
                continue;
            }
            else if (ApproveValues.Contains(decision))
            {
                reviewed = true;
                approved++;
            }
            else if (RejectValues.Contains(decision))
            {
                reviewed = false;
                rejected++;
            }
            else
            {
                errors.Add($"Dòng {line} ({id}): cột '{ReviewCsvColumns.Decision}' = '{Cell(decisionColumn)}' không hiểu — dùng 'x' (duyệt), 'loai' (không dùng) hoặc để trống.");
                continue;
            }

            var newMeaning = KrdictVocabMapper.Normalize(Cell(meaningColumn));
            if (newMeaning is not null)
            {
                meaningChanged++;
            }

            merged[id] = new VocabReview(
                Id: id,
                Reviewed: reviewed,
                Vi: newMeaning,
                DefinitionVi: KrdictVocabMapper.Normalize(Cell(definitionColumn)),
                Note: KrdictVocabMapper.Normalize(Cell(noteColumn)));
        }

        if (errors.Count > 0)
        {
            return new ReviewImportResult([], 0, 0, 0, 0, errors);
        }

        // Thứ tự ổn định theo thứ tự từ vựng (diff gọn)
        var order = vocabulary.Select((v, index) => (v.Id, index)).ToDictionary(x => x.Id, x => x.index, StringComparer.Ordinal);
        var reviews = merged.Values.OrderBy(r => order.GetValueOrDefault(r.Id, int.MaxValue)).ThenBy(r => r.Id, StringComparer.Ordinal).ToList();
        return new ReviewImportResult(reviews, approved, rejected, undecided, meaningChanged, []);

        static ReviewImportResult Failed(string error) => new([], 0, 0, 0, 0, [error]);
    }

    /// <summary>Đọc CSV: thử UTF-8 trước; nếu không hợp lệ (Excel lưu kiểu "CSV" thường bằng mã hóa Windows) → báo lỗi rõ ràng.</summary>
    public static string ReadCsvText(string path)
    {
        var bytes = File.ReadAllBytes(path);
        try
        {
            return new UTF8Encoding(encoderShouldEmitUTF8Identifier: false, throwOnInvalidBytes: true).GetString(bytes);
        }
        catch (DecoderFallbackException)
        {
            throw new InvalidDataException(string.Create(CultureInfo.InvariantCulture,
                $"'{path}' không phải UTF-8. Trong Excel chọn File → Save As → \"CSV UTF-8 (Comma delimited)\", hoặc tải từ Google Sheets (File → Download → CSV)."));
        }
    }
}
