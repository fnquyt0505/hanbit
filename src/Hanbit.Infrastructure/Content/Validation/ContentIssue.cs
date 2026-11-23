namespace Hanbit.Infrastructure.Content.Validation;

/// <summary>Một lỗi dữ liệu nội dung: file nào, trường nào (JSON path snake_case), vì sao.</summary>
/// <param name="File">Đường dẫn file, VD <c>content/courses/beginner/lessons/01-chao-hoi.json</c>.</param>
/// <param name="Path">Vị trí trong file, VD <c>exercises[2].answer_index</c>; null = lỗi cả file.</param>
public sealed record ContentIssue(string File, string? Path, string Message)
{
    public override string ToString() => Path is null ? $"{File}: {Message}" : $"{File}: {Path}: {Message}";
}

/// <summary>Kết quả kiểm tra thư mục content/.</summary>
public sealed record ContentValidationResult(int FileCount, IReadOnlyList<ContentIssue> Issues)
{
    public bool IsValid => Issues.Count == 0;
}
