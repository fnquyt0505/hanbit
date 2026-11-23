namespace Hanbit.ContentImporter.Krdict;

/// <summary>Một trang kết quả của /api/search.</summary>
public sealed record KrdictSearchPage(int Total, int Start, int Num, IReadOnlyList<KrdictEntry> Items);

/// <summary>Một mục từ trong kết quả tìm kiếm (giữ nguyên chữ Hàn như API trả về).</summary>
/// <param name="TargetCode">Mã mục từ (ổn định) → id <c>kd-&lt;target_code&gt;</c>.</param>
/// <param name="Origin">Nguyên ngữ: Hán tự (VD 學校) hoặc từ gốc ngoại lai.</param>
/// <param name="WordGrade">초급 / 중급 / 고급 / rỗng.</param>
/// <param name="Pos">Từ loại bằng tiếng Hàn, VD 명사.</param>
public sealed record KrdictEntry(
    string TargetCode,
    string Word,
    int SupNo,
    string? Origin,
    string? Pronunciation,
    string? WordGrade,
    string Pos,
    string Link,
    IReadOnlyList<KrdictSense> Senses);

/// <summary>Một nghĩa: định nghĩa tiếng Hàn + bản dịch (tiếng Việt khi trans_lang=7).</summary>
public sealed record KrdictSense(int Order, string Definition, string? TransWord, string? TransDefinition);

/// <summary>API trả về &lt;error&gt; (VD 010 = hết lượt gọi trong ngày, 020 = key không hợp lệ).</summary>
public sealed class KrdictApiException(string code, string message)
    : Exception($"krdict API lỗi {code}: {message}")
{
    public string Code { get; } = code;

    public bool IsDailyLimitExceeded => Code == "010";
}
