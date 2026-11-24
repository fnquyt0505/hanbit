using System.Text.Json;
using System.Text.Json.Serialization;

namespace Hanbit.Web.Tests;

/// <summary>JSON giống API (camelCase + enum chuỗi camelCase) để đọc phản hồi trong test.</summary>
internal static class TestJson
{
    public static readonly JsonSerializerOptions Options = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };
}
