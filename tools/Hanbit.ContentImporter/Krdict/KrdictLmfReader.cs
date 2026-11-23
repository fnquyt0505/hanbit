using System.Globalization;
using System.Text.Json;

namespace Hanbit.ContentImporter.Krdict;

/// <summary>
/// Đọc file JSON "사전 전체 내려받기" của 한국어기초사전 (định dạng LMF — Lexical Markup Framework),
/// cấu trúc đã xác minh với dữ liệu thật 2026-11-24 (ARCHITECTURE §8):
/// <c>LexicalResource.Lexicon.LexicalEntry[]</c>; mỗi mục: <c>val</c> = mã mục từ, <c>feat</c> = [{att, val}…]
/// (homonym_number, lexicalUnit, partOfSpeech, origin, vocabularyLevel…), <c>Lemma</c> (object hoặc mảng: writtenForm + variant),
/// <c>WordForm</c> (object/mảng/vắng; type 발음 → pronunciation), <c>Sense[]</c> (val = thứ tự, feat definition,
/// <c>Equivalent[]</c> theo ngôn ngữ: language / lemma / definition).
/// Mọi chỗ "object hoặc mảng" được đọc phòng thủ.
/// </summary>
public static class KrdictLmfReader
{
    public const string VietnameseLanguage = "베트남어";

    public static async Task<IReadOnlyList<KrdictEntry>> ReadAsync(string path, CancellationToken cancellationToken)
    {
        await using var stream = File.OpenRead(path);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        if (!doc.RootElement.TryGetProperty("LexicalResource", out var resource)
            || !resource.TryGetProperty("Lexicon", out var lexicon)
            || !lexicon.TryGetProperty("LexicalEntry", out var lexicalEntries))
        {
            throw new FormatException($"{Path.GetFileName(path)}: không phải file LMF của 한국어기초사전 (thiếu LexicalResource.Lexicon.LexicalEntry).");
        }

        var entries = new List<KrdictEntry>();
        foreach (var entry in Items(lexicalEntries))
        {
            if (ParseEntry(entry) is { } parsed)
            {
                entries.Add(parsed);
            }
        }

        return entries;
    }

    private static KrdictEntry? ParseEntry(JsonElement entry)
    {
        var targetCode = String(entry, "val");
        var word = Items(Property(entry, "Lemma")).Select(lemma => Feature(lemma, "writtenForm")).FirstOrDefault(w => w is not null);
        if (targetCode is null || word is null)
        {
            return null;
        }

        var pronunciations = Items(Property(entry, "WordForm"))
            .Where(form => Feature(form, "type") is null or "발음")
            .Select(form => Feature(form, "pronunciation"))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var senses = Items(Property(entry, "Sense")).Select(ParseSense).ToList();

        return new KrdictEntry(
            TargetCode: targetCode,
            Word: word,
            SupNo: int.TryParse(Feature(entry, "homonym_number"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var supNo) ? supNo : 0,
            Origin: Feature(entry, "origin"),
            Pronunciation: pronunciations.Count == 0 ? null : string.Join('/', pronunciations),
            WordGrade: Feature(entry, "vocabularyLevel"),
            Pos: Feature(entry, "partOfSpeech") ?? string.Empty,
            Link: $"https://krdict.korean.go.kr/kor/dicSearch/SearchView?ParaWordNo={targetCode}",
            Senses: senses,
            LexicalUnit: Feature(entry, "lexicalUnit"));
    }

    private static KrdictSense ParseSense(JsonElement sense, int index)
    {
        var vietnamese = Items(Property(sense, "Equivalent")).FirstOrDefault(eq => Feature(eq, "language") == VietnameseLanguage);
        var hasVietnamese = vietnamese.ValueKind == JsonValueKind.Object;
        return new KrdictSense(
            Order: int.TryParse(String(sense, "val"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var order) ? order : index + 1,
            Definition: Feature(sense, "definition") ?? string.Empty,
            TransWord: hasVietnamese ? Feature(vietnamese, "lemma") : null,
            TransDefinition: hasVietnamese ? Feature(vietnamese, "definition") : null);
    }

    /// <summary>Giá trị <c>val</c> của phần tử <c>feat</c> có <c>att</c> = <paramref name="att"/> (feat là object hoặc mảng).</summary>
    private static string? Feature(JsonElement element, string att) =>
        Items(Property(element, "feat"))
            .Where(f => String(f, "att") == att)
            .Select(f => String(f, "val"))
            .FirstOrDefault(v => !string.IsNullOrWhiteSpace(v))
            ?.Trim();

    private static JsonElement Property(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) ? value : default;

    private static string? String(JsonElement element, string name) =>
        Property(element, name) is { ValueKind: JsonValueKind.String } value ? value.GetString() : null;

    /// <summary>Object → 1 phần tử; mảng → các phần tử; vắng → rỗng.</summary>
    private static IEnumerable<JsonElement> Items(JsonElement element) => element.ValueKind switch
    {
        JsonValueKind.Array => element.EnumerateArray(),
        JsonValueKind.Object => [element],
        _ => [],
    };
}
