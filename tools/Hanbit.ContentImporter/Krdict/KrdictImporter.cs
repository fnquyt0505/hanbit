using System.Collections.Concurrent;
using System.Text.Json;

using Hanbit.Domain.Content;
using Hanbit.Infrastructure.Content;

namespace Hanbit.ContentImporter.Krdict;

/// <summary>Kết quả import để báo cáo (DoD: báo cáo số từ).</summary>
public sealed record KrdictImportReport(
    int SyllablesQueried,
    int PagesFetched,
    int EntriesFound,
    int ImportedCount,
    IReadOnlyList<string> SkippedWithoutVietnamese,
    IReadOnlyDictionary<PartOfSpeech, int> CountByPos);

/// <summary>
/// Quét mọi âm tiết Hangul (가…힣) bằng tìm kiếm "bắt đầu bằng" + lọc cấp độ → toàn bộ từ của cấp đó
/// (API không hỗ trợ từ khóa rỗng/ký tự đại diện — đã thử). Gộp theo target_code, map sang VocabItem, sắp xếp ổn định.
/// </summary>
public sealed class KrdictImporter(IKrdictSearchSource source, Action<string>? log = null)
{
    public const char FirstSyllable = '가';
    public const char LastSyllable = '힣';

    public static string LevelParameter(VocabGrade grade) => grade switch
    {
        VocabGrade.Beginner => "level1",
        VocabGrade.Intermediate => "level2",
        VocabGrade.Advanced => "level3",
        _ => throw new ArgumentOutOfRangeException(nameof(grade), grade, "Chỉ hỗ trợ beginner/intermediate/advanced."),
    };

    public async Task<(IReadOnlyList<VocabItem> Items, KrdictImportReport Report)> ImportAsync(
        VocabGrade grade,
        IEnumerable<char> syllables,
        int maxConcurrency,
        CancellationToken cancellationToken)
    {
        var level = LevelParameter(grade);
        var syllableList = syllables.ToList();
        var entries = new ConcurrentDictionary<string, KrdictEntry>(StringComparer.Ordinal);
        var pages = 0;
        var done = 0;

        await Parallel.ForEachAsync(
            syllableList,
            new ParallelOptions { MaxDegreeOfParallelism = maxConcurrency, CancellationToken = cancellationToken },
            async (syllable, ct) =>
            {
                for (var page = 1; ; page++)
                {
                    var result = KrdictXmlParser.ParseSearch(await source.GetStartsWithPageAsync(syllable, level, page, ct));
                    Interlocked.Increment(ref pages);
                    foreach (var entry in result.Items.Where(e => KrdictVocabMapper.MapGrade(e.WordGrade) == grade))
                    {
                        entries.TryAdd(entry.TargetCode, entry);
                    }

                    if (result.Items.Count == 0 || page * KrdictHttpSearchSource.PageSize >= result.Total)
                    {
                        break;
                    }
                }

                var count = Interlocked.Increment(ref done);
                if (count % 1000 == 0 || count == syllableList.Count)
                {
                    log?.Invoke($"  … {count}/{syllableList.Count} âm tiết, {entries.Count} mục từ");
                }
            });

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
            SyllablesQueried: syllableList.Count,
            PagesFetched: pages,
            EntriesFound: entries.Count,
            ImportedCount: sorted.Count,
            SkippedWithoutVietnamese: skipped.Order(StringComparer.Ordinal).ToList(),
            CountByPos: sorted.GroupBy(v => v.Pos).OrderByDescending(g => g.Count()).ToDictionary(g => g.Key, g => g.Count()));

        return (sorted, report);
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

    public static IEnumerable<char> AllSyllables()
    {
        for (var c = FirstSyllable; c <= LastSyllable; c++)
        {
            yield return c;
        }
    }
}
