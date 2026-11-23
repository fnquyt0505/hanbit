using System.Text.Json;

using Hanbit.Domain.Content;
using Hanbit.Infrastructure.Content;

namespace Hanbit.ContentImporter.Krdict;

/// <summary>Kết quả import để báo cáo (DoD: báo cáo số từ).</summary>
public sealed record KrdictImportReport(
    int FilesRead,
    int EntriesScanned,
    int MatchingGrade,
    IReadOnlyDictionary<string, int> ExcludedNonWordUnits,
    int ImportedCount,
    IReadOnlyList<string> SkippedWithoutVietnamese,
    IReadOnlyDictionary<PartOfSpeech, int> CountByPos);

/// <summary>
/// Nhập từ vựng một cấp độ từ các file LMF "사전 전체 내려받기": lọc đúng cấp + chỉ <c>단어</c>
/// (cụm từ/quán ngữ/tục ngữ/mẫu ngữ pháp soạn riêng), gộp theo mã mục từ, map sang <see cref="VocabItem"/>, sắp xếp ổn định.
/// </summary>
public static class KrdictImporter
{
    public const string WordUnit = "단어";

    public static async Task<(IReadOnlyList<VocabItem> Items, KrdictImportReport Report)> ImportAsync(
        VocabGrade grade,
        IReadOnlyList<string> files,
        CancellationToken cancellationToken)
    {
        var entries = new Dictionary<string, KrdictEntry>(StringComparer.Ordinal);
        var excludedUnits = new Dictionary<string, int>(StringComparer.Ordinal);
        var scanned = 0;
        var matching = 0;

        foreach (var file in files)
        {
            foreach (var entry in await KrdictLmfReader.ReadAsync(file, cancellationToken))
            {
                scanned++;
                if (KrdictVocabMapper.MapGrade(entry.WordGrade) != grade)
                {
                    continue;
                }

                matching++;
                if (entry.LexicalUnit != WordUnit)
                {
                    var unit = entry.LexicalUnit ?? "(không rõ)";
                    excludedUnits[unit] = excludedUnits.GetValueOrDefault(unit) + 1;
                    continue;
                }

                entries.TryAdd(entry.TargetCode, entry);
            }
        }

        var items = new List<VocabItem>();
        var skipped = new List<string>();
        foreach (var entry in entries.Values)
        {
            if (KrdictVocabMapper.Map(entry) is { } item)
            {
                items.Add(item);
            }
            else
            {
                skipped.Add($"{entry.Word} (kd-{entry.TargetCode})");
            }
        }

        // Thứ tự ổn định (diff gọn khi import lại): theo từ, rồi mã mục từ
        var sorted = items
            .OrderBy(v => v.Ko, StringComparer.Ordinal)
            .ThenBy(v => int.TryParse(v.Source.Id, out var code) ? code : int.MaxValue)
            .ToList();

        var report = new KrdictImportReport(
            FilesRead: files.Count,
            EntriesScanned: scanned,
            MatchingGrade: matching,
            ExcludedNonWordUnits: excludedUnits,
            ImportedCount: sorted.Count,
            SkippedWithoutVietnamese: skipped.Order(StringComparer.Ordinal).ToList(),
            CountByPos: sorted.GroupBy(v => v.Pos).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()));

        return (sorted, report);
    }

    /// <summary>File LMF trong <paramref name="input"/> (1 file .json hoặc thư mục chứa các file .json).</summary>
    public static IReadOnlyList<string> ResolveInputFiles(string input)
    {
        if (File.Exists(input))
        {
            return [input];
        }

        if (Directory.Exists(input))
        {
            var files = Directory.GetFiles(input, "*.json", SearchOption.TopDirectoryOnly).Order(StringComparer.Ordinal).ToList();
            return files.Count > 0 ? files : throw new FileNotFoundException($"Thư mục '{input}' không có file .json nào.");
        }

        throw new FileNotFoundException($"Không tìm thấy file/thư mục đầu vào '{input}'.");
    }

    /// <summary>Ghi file vocab. Từ chối ghi vào vocab-reviews.json (dữ liệu của người duyệt — RULES §7).</summary>
    public static async Task WriteAsync(string outputPath, IReadOnlyList<VocabItem> items, CancellationToken cancellationToken)
    {
        if (string.Equals(Path.GetFileName(outputPath), ContentPaths.VocabReviews, StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"Không được ghi đè {ContentPaths.VocabReviews} — file này chỉ người duyệt sửa.");
        }

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outputPath))!);
        var temp = outputPath + ".tmp";
        await using (var stream = File.Create(temp))
        {
            await JsonSerializer.SerializeAsync(stream, items, ContentJson.Options, cancellationToken);
        }

        File.Move(temp, outputPath, overwrite: true);
    }
}
