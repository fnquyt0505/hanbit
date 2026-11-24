using System.Net;
using System.Net.Http.Json;
using System.Text;

using Hanbit.Application.Abstractions;
using Hanbit.Domain.Progress;
using Hanbit.Domain.Srs;
using Hanbit.Web.Controllers.Api;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>T6.2: POST /api/reviews/due + POST /api/reviews/grade (đồng hồ cố định).</summary>
public class ReviewsApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 10, 0, 0, TimeSpan.Zero);
    private static readonly DateOnly Today = new(2026, 11, 24);

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private HttpClient CreateClient() => factory
        .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IClock, FixedClock>()))
        .CreateClient();

    private static ReviewCard Card(string id, DateTimeOffset due, int interval = 0, int reps = 0) =>
        new(id, ItemType.Vocab, 2.5, interval, reps, due, null);

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    [Fact]
    public async Task Due_ReturnsOnlyDueReviewedWords_EarliestFirst_WithWordAndPreviews()
    {
        using var client = CreateClient();
        var cards = new[]
        {
            Card("kd-44487", Now.AddHours(-1)),   // 가족 — đến hạn
            Card("kd-17298", Now.AddDays(-2)),    // 안녕 — đến hạn sớm hơn
            Card("kd-62256", Now.AddDays(1)),     // 가수 — chưa đến hạn
            Card("kd-1", Now.AddDays(-1)),        // không có trong nội dung
        };

        var result = await ReadAsync<DueReviewsResponse>(
            await client.PostAsJsonAsync("/api/reviews/due", new DueReviewsRequest(cards, null), TestJson.Options));

        result.Total.ShouldBe(2);
        result.Today.ShouldBe(Today);
        result.Items.Select(i => i.Card.ItemId).ShouldBe(["kd-17298", "kd-44487"]);
        result.Items[1].Word.Ko.ShouldBe("가족");
        result.Items[1].Word.Vi.ShouldBe("gia đình");
        result.Items[1].Word.Romanization.ShouldBe("gajok");
        result.Items[1].Previews.ShouldBe(new Dictionary<string, int> { ["again"] = 0, ["hard"] = 1, ["good"] = 1, ["easy"] = 3 });
    }

    [Fact]
    public async Task Due_RespectsLimit_ButReportsTotal()
    {
        using var client = CreateClient();
        var cards = new[] { Card("kd-44487", Now), Card("kd-17298", Now), Card("kd-62256", Now) };

        var result = await ReadAsync<DueReviewsResponse>(
            await client.PostAsJsonAsync("/api/reviews/due", new DueReviewsRequest(cards, 2), TestJson.Options));

        result.Total.ShouldBe(3);
        result.Items.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Due_NoCards_EmptyResult()
    {
        using var client = CreateClient();

        var result = await ReadAsync<DueReviewsResponse>(
            await client.PostAsJsonAsync("/api/reviews/due", new DueReviewsRequest(null, null), TestJson.Options));

        result.Total.ShouldBe(0);
        result.Items.ShouldBeEmpty();
    }

    [Fact]
    public async Task Grade_Good_SchedulesAndAddsXpAndReview()
    {
        using var client = CreateClient();
        var request = new GradeReviewRequest(Card("kd-44487", Now, interval: 1, reps: 1), ReviewGrade.Good,
            [new DailyActivity(Today.AddDays(-1), 20, 0), new DailyActivity(Today, 5, 2)]);

        var result = await ReadAsync<GradeReviewResponse>(
            await client.PostAsJsonAsync("/api/reviews/grade", request, TestJson.Options));

        result.Card.IntervalDays.ShouldBe(3);
        result.Card.Repetitions.ShouldBe(2);
        result.Card.LastReviewedAt.ShouldBe(Now);
        result.Xp.ShouldBe(1);
        result.Activity.Last().ShouldBe(new DailyActivity(Today, 6, 3));
        result.Streak.Current.ShouldBe(2);
    }

    [Fact]
    public async Task Grade_AcceptsLowercaseGradeString()
    {
        using var client = CreateClient();
        const string json = """
            { "card": { "itemId": "kd-1", "itemType": "vocab", "ease": 2.5, "intervalDays": 0, "repetitions": 0,
                        "dueAt": "2026-11-24T10:00:00Z", "lastReviewedAt": null },
              "grade": "again", "activity": [] }
            """;

        var result = await ReadAsync<GradeReviewResponse>(
            await client.PostAsync("/api/reviews/grade", new StringContent(json, Encoding.UTF8, "application/json")));

        result.Card.DueAt.ShouldBe(Now);
        result.Card.Repetitions.ShouldBe(0);
    }

    [Theory]
    [InlineData("""{ "grade": "good" }""")]
    [InlineData("""{ "card": { "itemId": "kd-1", "itemType": "vocab", "ease": 2.5, "intervalDays": 0, "repetitions": 0, "dueAt": "2026-11-24T10:00:00Z" } }""")]
    [InlineData("""{ "card": { "itemId": "kd-1", "itemType": "vocab", "ease": 2.5, "intervalDays": 0, "repetitions": 0, "dueAt": "2026-11-24T10:00:00Z" }, "grade": "perfect" }""")]
    public async Task Grade_InvalidRequest_Returns400(string json)
    {
        using var client = CreateClient();

        using var response = await client.PostAsync("/api/reviews/grade", new StringContent(json, Encoding.UTF8, "application/json"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
