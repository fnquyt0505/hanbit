using System.Text.Json.Serialization;

namespace Hanbit.Domain.Content;

/// <summary>
/// Bài tập tự soạn trong file bài học. Trường <c>type</c> trong JSON chọn loại cụ thể.
/// Các record này CHỨA ĐÁP ÁN → chỉ dùng ở server; gửi xuống trình duyệt phải qua DTO không có đáp án.
/// </summary>
/// <param name="Id">Id duy nhất trong bài học.</param>
/// <param name="ExplanationVi">Giải thích hiện sau khi trả lời (tùy chọn).</param>
[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(MultipleChoiceExercise), "multiple_choice")]
[JsonDerivedType(typeof(ListenChooseExercise), "listen_choose")]
[JsonDerivedType(typeof(MatchPairsExercise), "match_pairs")]
[JsonDerivedType(typeof(ReorderExercise), "reorder")]
[JsonDerivedType(typeof(TypeAnswerExercise), "type_answer")]
public abstract record Exercise(string Id, string? ExplanationVi = null);

/// <summary>Chọn 1 đáp án đúng trong các lựa chọn.</summary>
/// <param name="AnswerIndex">Vị trí (từ 0) của đáp án đúng trong <paramref name="Choices"/>.</param>
public sealed record MultipleChoiceExercise(
    string Id,
    string Prompt,
    IReadOnlyList<string> Choices,
    int AnswerIndex,
    string? ExplanationVi = null)
    : Exercise(Id, ExplanationVi);

/// <summary>Nghe câu/từ tiếng Hàn rồi chọn đáp án.</summary>
/// <param name="AudioKo">Văn bản tiếng Hàn được đọc lên (Web Speech API / MP3).</param>
/// <param name="AnswerIndex">Vị trí (từ 0) của đáp án đúng trong <paramref name="Choices"/>.</param>
public sealed record ListenChooseExercise(
    string Id,
    string AudioKo,
    IReadOnlyList<string> Choices,
    int AnswerIndex,
    string? ExplanationVi = null)
    : Exercise(Id, ExplanationVi);

/// <summary>Nối cặp (VD tiếng Hàn ↔ nghĩa tiếng Việt). Thứ tự hiển thị được xáo khi gửi xuống trình duyệt.</summary>
public sealed record MatchPairsExercise(
    string Id,
    string Prompt,
    IReadOnlyList<MatchPair> Pairs,
    string? ExplanationVi = null)
    : Exercise(Id, ExplanationVi);

public sealed record MatchPair(string Left, string Right);

/// <summary>Sắp xếp các mảnh thành câu đúng.</summary>
/// <param name="Tokens">Các mảnh theo THỨ TỰ ĐÚNG (đáp án); được xáo khi gửi xuống trình duyệt.</param>
public sealed record ReorderExercise(
    string Id,
    string Prompt,
    IReadOnlyList<string> Tokens,
    string? ExplanationVi = null)
    : Exercise(Id, ExplanationVi);

/// <summary>Gõ câu trả lời; so sánh sau chuẩn hóa NFC + khoảng trắng.</summary>
/// <param name="AcceptedAnswers">Các đáp án được chấp nhận (ít nhất 1).</param>
public sealed record TypeAnswerExercise(
    string Id,
    string Prompt,
    IReadOnlyList<string> AcceptedAnswers,
    string? ExplanationVi = null)
    : Exercise(Id, ExplanationVi);
