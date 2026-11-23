namespace Hanbit.Domain.Content;

/// <summary>Cấp độ theo 한국어기초사전: 초급 / 중급 / 고급 (None: không xếp cấp).</summary>
public enum VocabGrade
{
    Beginner,
    Intermediate,
    Advanced,
    None,
}

/// <summary>Từ loại (품사) theo 한국어기초사전 — 15 giá trị khớp tài liệu Open API (đã xác minh T1.5).</summary>
public enum PartOfSpeech
{
    Noun,               // 명사
    DependentNoun,      // 의존 명사
    Pronoun,            // 대명사
    Numeral,            // 수사
    Verb,               // 동사
    AuxiliaryVerb,      // 보조 동사
    Adjective,          // 형용사
    AuxiliaryAdjective, // 보조 형용사
    Determiner,         // 관형사
    Adverb,             // 부사
    Interjection,       // 감탄사
    Particle,           // 조사
    Ending,             // 어미
    Affix,              // 접사
    None,               // 품사 없음
}

/// <summary>Ghi công nguồn dữ liệu (bắt buộc với dữ liệu CC BY-SA).</summary>
/// <param name="Name">VD <c>한국어기초사전</c>.</param>
/// <param name="Id">Mã mục từ ở nguồn (target_code).</param>
/// <param name="Url">Link tới mục từ gốc.</param>
/// <param name="License">VD <c>CC BY-SA 2.0 KR</c>.</param>
public sealed record SourceRef(string Name, string Id, string Url, string License);

/// <summary>Câu ví dụ song ngữ.</summary>
public sealed record Example(string Ko, string Vi);

/// <summary>Một mục từ vựng (content/vocab/*.json — sinh bởi ContentImporter). Trường có <c>= null</c> được bỏ trống trong JSON.</summary>
/// <param name="Id">Id ổn định, VD <c>kd-12345</c>.</param>
/// <param name="Reviewed">Nghĩa tiếng Việt đã được người duyệt → mới được dùng trong bài học.</param>
/// <param name="Pronunciation">Phát âm theo Hangul, VD <c>[함니다]</c>.</param>
public sealed record VocabItem(
    string Id,
    string Ko,
    string Vi,
    PartOfSpeech Pos,
    VocabGrade Grade,
    IReadOnlyList<Example> Examples,
    bool Reviewed,
    SourceRef Source,
    string? Pronunciation = null,
    string? Romanization = null,
    string? DefinitionVi = null,
    string? Hanja = null,
    string? Audio = null);

/// <summary>Một điểm ngữ pháp tự biên soạn (content/grammar/*.json).</summary>
/// <param name="Pattern">Mẫu ngữ pháp, VD <c>-아요/어요</c>.</param>
public sealed record GrammarPoint(
    string Id,
    string Pattern,
    string MeaningVi,
    string ExplanationVi,
    IReadOnlyList<Example> Examples,
    VocabGrade Grade);
