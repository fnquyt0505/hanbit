using System.Globalization;
using System.Xml.Linq;

namespace Hanbit.ContentImporter.Krdict;

/// <summary>
/// Đọc XML của krdict /api/search (cấu trúc đã xác minh với API thật 2026-11-24 — ARCHITECTURE §8).
/// Đọc phòng thủ: phần tử tùy chọn có thể vắng; chuỗi được Trim (API chèn nhiều khoảng trắng/xuống dòng).
/// </summary>
public static class KrdictXmlParser
{
    public static KrdictSearchPage ParseSearch(string xml)
    {
        var doc = XDocument.Parse(xml);
        var root = doc.Root ?? throw new FormatException("XML krdict rỗng.");

        if (root.Name.LocalName == "error")
        {
            throw new KrdictApiException(Text(root, "error_code") ?? "?", Text(root, "message") ?? "(không có thông báo)");
        }

        if (root.Name.LocalName != "channel")
        {
            throw new FormatException($"XML krdict không mong đợi: phần tử gốc <{root.Name.LocalName}>.");
        }

        var items = root.Elements("item").Select(ParseItem).ToList();
        return new KrdictSearchPage(Int(root, "total"), Int(root, "start"), Int(root, "num"), items);
    }

    private static KrdictEntry ParseItem(XElement item) => new(
        TargetCode: Required(item, "target_code"),
        Word: Required(item, "word"),
        SupNo: int.TryParse(Text(item, "sup_no"), NumberStyles.Integer, CultureInfo.InvariantCulture, out var supNo) ? supNo : 0,
        Origin: Text(item, "origin"),
        Pronunciation: Text(item, "pronunciation"),
        WordGrade: Text(item, "word_grade"),
        Pos: Text(item, "pos") ?? string.Empty,
        Link: Text(item, "link") ?? string.Empty,
        Senses: item.Elements("sense").Select(ParseSense).ToList());

    private static KrdictSense ParseSense(XElement sense)
    {
        var translation = sense.Element("translation");
        return new KrdictSense(
            Order: Int(sense, "sense_order"),
            Definition: Text(sense, "definition") ?? string.Empty,
            TransWord: translation is null ? null : Text(translation, "trans_word"),
            TransDefinition: translation is null ? null : Text(translation, "trans_dfn"));
    }

    private static string? Text(XElement parent, string name)
    {
        var value = parent.Element(name)?.Value.Trim();
        return string.IsNullOrEmpty(value) ? null : value;
    }

    private static string Required(XElement parent, string name) =>
        Text(parent, name) ?? throw new FormatException($"Thiếu <{name}> trong <{parent.Name.LocalName}>.");

    private static int Int(XElement parent, string name) =>
        int.TryParse(Text(parent, name), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : 0;
}
