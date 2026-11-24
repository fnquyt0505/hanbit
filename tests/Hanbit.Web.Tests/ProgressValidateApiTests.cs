using System.Net;
using System.Net.Http.Json;
using System.Text;

using Hanbit.Web.Controllers.Api;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>T5.5: POST /api/progress/validate (kiểm tra + migrate + làm sạch) và trang /profile.</summary>
public class ProgressValidateApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string ValidBackup = """
        {
          "schemaVersion": 1,
          "createdAt": "2026-11-20T08:00:00+00:00",
          "lessons": { "beginner/chao-hoi": { "status": "completed", "bestScore": 85, "completedAt": "2026-11-21T08:00:00+00:00" } },
          "cards": { "kd-17298": { "itemId": "kd-17298", "itemType": "vocab", "ease": 2.5, "intervalDays": 1, "repetitions": 1,
                                   "dueAt": "2026-11-22T17:00:00+00:00", "lastReviewedAt": "2026-11-21T08:00:00+00:00" } },
          "activity": [ { "date": "2026-11-21", "xp": 24, "reviews": 0 } ],
          "totalXp": 24
        }
        """;

    private static StringContent Json(string json) => new(json, Encoding.UTF8, "application/json");

    private async Task<HttpResponseMessage> PostAsync(string json)
    {
        using var client = factory.CreateClient();
        return await client.PostAsync("/api/progress/validate", Json(json));
    }

    [Fact]
    public async Task Validate_ValidBackup_ReturnsSameProgressWithoutWarnings()
    {
        using var response = await PostAsync(ValidBackup);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProgressValidateResponse>(TestJson.Options);
        body.ShouldNotBeNull();
        body.Warnings.ShouldBeEmpty();
        body.Progress.Lessons["beginner/chao-hoi"].BestScore.ShouldBe(85);
        body.Progress.Cards["kd-17298"].IntervalDays.ShouldBe(1);
        body.Progress.TotalXp.ShouldBe(24);

        var json = await response.Content.ReadAsStringAsync();
        json.ShouldContain("\"schemaVersion\":1");
        json.ShouldContain("\"itemType\":\"vocab\"");
        json.ShouldContain("\"date\":\"2026-11-21\"");
    }

    [Fact]
    public async Task Validate_OutOfRangeValues_ClampedWithWarnings()
    {
        var tampered = ValidBackup.Replace("\"ease\": 2.5", "\"ease\": 42", StringComparison.Ordinal)
            .Replace("\"totalXp\": 24", "\"totalXp\": -5", StringComparison.Ordinal);

        using var response = await PostAsync(tampered);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<ProgressValidateResponse>(TestJson.Options);
        body!.Progress.Cards["kd-17298"].Ease.ShouldBe(5.0);
        body.Progress.TotalXp.ShouldBe(0);
        body.Warnings.Count.ShouldBeGreaterThanOrEqualTo(2);
    }

    [Theory]
    [InlineData("""{ "schemaVersion": 99, "lessons": {} }""", "mới hơn")]
    [InlineData("""{ "lessons": {} }""", "schemaVersion")]
    [InlineData("""[1, 2, 3]""", "đối tượng JSON")]
    [InlineData("""{ "schemaVersion": 1, "lessons": "sai-kieu", "cards": {}, "activity": [], "totalXp": 0, "createdAt": "2026-11-20T00:00:00Z" }""", "định dạng")]
    public async Task Validate_InvalidFile_Returns400WithVietnameseMessage(string json, string expected)
    {
        using var response = await PostAsync(json);

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem!.Errors["file"].ShouldHaveSingleItem().ShouldContain(expected);
    }

    [Fact]
    public async Task Validate_NotJson_Returns400()
    {
        using var response = await PostAsync("đây không phải JSON");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task ProfilePage_HasBackupSection_AndMarksNavCurrent()
    {
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/profile");

        html.ShouldContain("data-testid=\"backup\"");
        html.ShouldContain("/js/profile.js");
        html.ShouldContain("href=\"/profile\" aria-current=\"page\"");
    }
}
