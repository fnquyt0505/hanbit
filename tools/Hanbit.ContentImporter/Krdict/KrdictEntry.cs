namespace Hanbit.ContentImporter.Krdict;

/// <summary>Một mục từ 한국어기초사전 (giữ nguyên chữ Hàn như nguồn).</summary>
/// <param name="TargetCode">Mã mục từ (ổn định) → id <c>kd-&lt;target_code&gt;</c>.</param>
/// <param name="SupNo">Số đồng âm (homonym_number).</param>
/// <param name="Origin">Nguyên ngữ: Hán tự (VD 學校) hoặc từ gốc ngoại lai.</param>
/// <param name="WordGrade">초급 / 중급 / 고급 / 없음 / null.</param>
/// <param name="Pos">Từ loại bằng tiếng Hàn, VD 명사 (rỗng nếu không có).</param>
/// <param name="LexicalUnit">단어 / 구 / 관용구 / 속담 / 문법‧표현.</param>
public sealed record KrdictEntry(
    string TargetCode,
    string Word,
    int SupNo,
    string? Origin,
    string? Pronunciation,
    string? WordGrade,
    string Pos,
    string Link,
    IReadOnlyList<KrdictSense> Senses,
    string? LexicalUnit = null);

/// <summary>Một nghĩa: định nghĩa tiếng Hàn + bản dịch tiếng Việt.</summary>
public sealed record KrdictSense(int Order, string Definition, string? TransWord, string? TransDefinition);
