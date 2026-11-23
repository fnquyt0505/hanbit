using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;

namespace Hanbit.Domain.Content;

/// <summary>
/// Cấu hình JSON chung cho dữ liệu nội dung (content/): đọc bởi JsonContentRepository,
/// ContentValidator; ghi bởi ContentImporter.
/// </summary>
public static class ContentJson
{
    /// <summary>
    /// snake_case cho tên trường và giá trị enum; đọc CHẶT để bắt lỗi dữ liệu sớm:
    /// thiếu trường bắt buộc, <c>null</c> ở trường không cho phép null, trường lạ (gõ sai) → <see cref="JsonException"/>.
    /// Cho phép comment + dấu phẩy cuối (file viết tay); <c>type</c> của Exercise không cần đứng đầu.
    /// Ghi: thụt lề, bỏ trường null, giữ nguyên chữ Hàn/Việt (không escape \uXXXX).
    /// </summary>
    public static JsonSerializerOptions Options { get; } = CreateOptions();

    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DictionaryKeyPolicy = JsonNamingPolicy.SnakeCaseLower,
            Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower, allowIntegerValues: false) },
            RespectNullableAnnotations = true,
            RespectRequiredConstructorParameters = true,
            UnmappedMemberHandling = JsonUnmappedMemberHandling.Disallow,
            AllowOutOfOrderMetadataProperties = true,
            ReadCommentHandling = JsonCommentHandling.Skip,
            AllowTrailingCommas = true,
            WriteIndented = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull, // trường tùy chọn null → bỏ khỏi file

            Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver(),
        };
        options.MakeReadOnly();
        return options;
    }
}
