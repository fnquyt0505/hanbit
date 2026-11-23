using Hanbit.Domain.Content;

namespace Hanbit.ContentImporter.Krdict;

/// <summary>Chuyển mục từ krdict → <see cref="VocabItem"/> (reviewed = false; người duyệt sửa qua vocab-reviews.json).</summary>
public static class KrdictVocabMapper
{
    public const string SourceName = "한국어기초사전";
    public const string License = "CC BY-SA 2.0 KR";

    /// <summary>Số nghĩa tiếng Việt tối đa ghép vào trường vi.</summary>
    private const int MaxMeanings = 3;

    /// <summary>품사 → <see cref="PartOfSpeech"/> (15 giá trị theo tài liệu Open API, đã xác minh).</summary>
    private static readonly Dictionary<string, PartOfSpeech> PartsOfSpeech = new(StringComparer.Ordinal)
    {
        ["명사"] = PartOfSpeech.Noun,
        ["의존 명사"] = PartOfSpeech.DependentNoun,
        ["대명사"] = PartOfSpeech.Pronoun,
        ["수사"] = PartOfSpeech.Numeral,
        ["동사"] = PartOfSpeech.Verb,
        ["보조 동사"] = PartOfSpeech.AuxiliaryVerb,
        ["형용사"] = PartOfSpeech.Adjective,
        ["보조 형용사"] = PartOfSpeech.AuxiliaryAdjective,
        ["관형사"] = PartOfSpeech.Determiner,
        ["부사"] = PartOfSpeech.Adverb,
        ["감탄사"] = PartOfSpeech.Interjection,
        ["조사"] = PartOfSpeech.Particle,
        ["어미"] = PartOfSpeech.Ending,
        ["접사"] = PartOfSpeech.Affix,
        ["품사 없음"] = PartOfSpeech.None,
    };

    public static VocabGrade MapGrade(string? wordGrade) => wordGrade switch
    {
        "초급" => VocabGrade.Beginner,
        "중급" => VocabGrade.Intermediate,
        "고급" => VocabGrade.Advanced,
        _ => VocabGrade.None,
    };

    public static PartOfSpeech MapPos(string pos) =>
        PartsOfSpeech.TryGetValue(pos.Trim(), out var value) ? value : PartOfSpeech.None;

    /// <summary>null nếu mục từ không có nghĩa tiếng Việt (không dùng được cho người học Việt).</summary>
    public static VocabItem? Map(KrdictEntry entry)
    {
        var meanings = entry.Senses
            .OrderBy(s => s.Order)
            .Select(s => Normalize(s.TransWord))
            .OfType<string>()
            .Distinct(StringComparer.Ordinal)
            .Take(MaxMeanings)
            .ToList();

        if (meanings.Count == 0)
        {
            return null;
        }

        var definition = entry.Senses.OrderBy(s => s.Order).Select(s => Normalize(s.TransDefinition)).FirstOrDefault(d => d is not null);

        return new VocabItem(
            Id: $"kd-{entry.TargetCode}",
            Ko: Normalize(entry.Word) ?? entry.Word,
            Vi: string.Join("; ", meanings),
            Pos: MapPos(entry.Pos),
            Grade: MapGrade(entry.WordGrade),
            Examples: [], // ví dụ krdict chỉ có tiếng Hàn — Example cần cả nghĩa Việt → để trống
            Reviewed: false,
            Source: new SourceRef(SourceName, entry.TargetCode, entry.Link, License),
            Pronunciation: Normalize(entry.Pronunciation),
            DefinitionVi: definition,
            Hanja: entry.Origin is not null && entry.Origin.Any(IsHan) ? entry.Origin : null);
    }

    /// <summary>NFC (so sánh đáp án ổn định — RULES §7) + gộp khoảng trắng thừa; rỗng → null.</summary>
    public static string? Normalize(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return null;
        }

        var collapsed = string.Join(' ', text.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries));
        return collapsed.Replace(" ,", ",", StringComparison.Ordinal).Normalize(System.Text.NormalizationForm.FormC);
    }

    private static bool IsHan(char c) => c is (>= '一' and <= '鿿') or (>= '㐀' and <= '䶿') or (>= '豈' and <= '﫿');
}
