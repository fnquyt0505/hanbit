using System.Text.Json;
using System.Text.Json.Nodes;

using Hanbit.Application.Abstractions;
using Hanbit.Domain.Progress;

namespace Hanbit.Application.Progress;

/// <summary>Kết quả nhập file tiến độ: thành công (tiến độ sạch + cảnh báo) hoặc lỗi.</summary>
public sealed record ProgressImportResult(LearnerProgress? Progress, IReadOnlyList<string> Warnings, string? Error)
{
    public static ProgressImportResult Fail(string error) => new(null, [], error);
}

/// <summary>
/// Nhập file sao lưu (<c>POST /api/progress/validate</c>): kiểm tra <c>schemaVersion</c> → migrate lên bản hiện tại
/// → đọc vào <see cref="LearnerProgress"/> → làm sạch (<see cref="ProgressSanitizer"/>). Đổi schema → thêm bước vào
/// <see cref="Migrations"/> + test (RULES).
/// </summary>
public sealed class ProgressImporter(IClock clock)
{
    /// <summary>Bước migrate: key = phiên bản nguồn, trả về JSON của phiên bản kế tiếp. Schema 1 là bản đầu tiên.</summary>
    private static readonly IReadOnlyDictionary<int, Func<JsonObject, JsonObject>> Migrations =
        new Dictionary<int, Func<JsonObject, JsonObject>>();

    public ProgressImportResult Import(JsonElement root, JsonSerializerOptions jsonOptions)
    {
        if (root.ValueKind != JsonValueKind.Object)
        {
            return ProgressImportResult.Fail("Tệp không phải bản sao lưu tiến độ Hanbit (cần một đối tượng JSON).");
        }

        if (!root.TryGetProperty("schemaVersion", out var versionElement) || !versionElement.TryGetInt32(out var version))
        {
            return ProgressImportResult.Fail("Tệp thiếu \"schemaVersion\" — không phải bản sao lưu tiến độ Hanbit.");
        }

        if (version > LearnerProgress.CurrentSchemaVersion)
        {
            return ProgressImportResult.Fail($"Tệp được tạo bởi phiên bản Hanbit mới hơn (schema {version}). Hãy tải lại trang rồi thử lại.");
        }

        if (version < 1)
        {
            return ProgressImportResult.Fail($"Phiên bản dữ liệu không hợp lệ (schema {version}).");
        }

        var node = JsonNode.Parse(root.GetRawText())!.AsObject();
        var warnings = new List<string>();
        while (version < LearnerProgress.CurrentSchemaVersion)
        {
            if (!Migrations.TryGetValue(version, out var migrate))
            {
                return ProgressImportResult.Fail($"Không chuyển đổi được dữ liệu schema {version}.");
            }

            node = migrate(node);
            version++;
            node["schemaVersion"] = version;
            warnings.Add($"Đã chuyển dữ liệu lên phiên bản {version}.");
        }

        LearnerProgress? progress;
        try
        {
            progress = node.Deserialize<LearnerProgress>(jsonOptions);
        }
        catch (JsonException)
        {
            return ProgressImportResult.Fail("Tệp không đúng định dạng tiến độ Hanbit (sai kiểu dữ liệu).");
        }

        if (progress is null)
        {
            return ProgressImportResult.Fail("Tệp rỗng.");
        }

        var sanitized = ProgressSanitizer.Sanitize(progress, clock.UtcNow, clock.Today);
        return new ProgressImportResult(sanitized.Progress, [.. warnings, .. sanitized.Warnings], null);
    }
}
