using System.Net;
using System.Net.Http.Json;

using Hanbit.Application.Abstractions;
using Hanbit.Domain.Progress;
using Hanbit.Web.Models.Learn;
using Hanbit.Web.Models.Progress;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>T5.2: POST /api/lessons/{c}/{l}/complete — chấm lại ở server, XP, thẻ ôn, streak theo giờ VN.</summary>
public class CompleteLessonApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static readonly DateTimeOffset Now = new(2026, 11, 24, 18, 0, 0, TimeSpan.Zero); // 01:00 ngày 25/11 giờ VN
    private const string Url = "/api/lessons/beginner/chao-hoi/complete";

    private sealed class FixedClock : IClock
    {
        public DateTimeOffset UtcNow => Now;
    }

    private HttpClient CreateClient() => factory
        .WithWebHostBuilder(b => b.ConfigureTestServices(s => s.AddSingleton<IClock, FixedClock>()))
        .CreateClient();

    private static SubmittedAnswerDto Choice(string id, int index) => new(id, new AnswerDto(index, null, null, null));

    private static async Task<CompleteLessonResponse> PostAsync(HttpClient client, CompleteLessonRequest request)
    {
        using var response = await client.PostAsJsonAsync(Url, request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<CompleteLessonResponse>(TestJson.Options))!;
    }

    [Fact]
    public async Task Complete_NoAnswers_ZeroScore_TenXp_EveryWordCarded()
    {
        using var client = CreateClient();

        var result = await PostAsync(client, new CompleteLessonRequest([], null, [], []));

        result.LessonKey.ShouldBe("beginner/chao-hoi");
        result.Total.ShouldBe(14);
        result.Correct.ShouldBe(0);
        result.Score.ShouldBe(0);
        result.Xp.ShouldBe(10);
        result.Today.ShouldBe(new DateOnly(2026, 11, 25));
        result.LessonRecord.Status.ShouldBe(LessonStatus.Completed);
        result.NewCards.Count.ShouldBe(14);
        result.NewCards.Count(c => c.DueAt == Now).ShouldBe(6); // 6 bài sinh 1 từ đều sai → đến hạn ngay
        result.Activity.ShouldBe([new DailyActivity(new DateOnly(2026, 11, 25), 10, 0)]);
        result.Streak.ShouldBe(new Streak(1, 1, true));
    }

    [Fact]
    public async Task Complete_RegradesAnswersOnServer()
    {
        using var client = CreateClient();
        var answers = new[]
        {
            Choice("nghia-gamsahamnida", 1), // đúng
            Choice("tam-biet-nguoi-ve", 1), // sai
            new SubmittedAnswerDto("go-bangapseumnida", new AnswerDto(null, null, null, "반갑습니다")), // đúng
        };

        var result = await PostAsync(client, new CompleteLessonRequest(answers, null, [], []));

        result.Correct.ShouldBe(2);
        result.Xp.ShouldBe(12);
        result.Score.ShouldBe(14);
    }

    [Fact]
    public async Task Complete_KeepsBestScore_DoesNotDuplicateExistingCards()
    {
        using var client = CreateClient();
        var existing = new ReviewCard("kd-17298", ItemType.Vocab, 2.5, 3, 2, Now.AddDays(3), Now.AddDays(-1));

        var result = await PostAsync(client, new CompleteLessonRequest(
            [], new LessonRecord(LessonStatus.Completed, 80, Now.AddDays(-2)), [existing], []));

        result.LessonRecord.BestScore.ShouldBe(80);
        result.NewCards.ShouldNotContain(c => c.ItemId == "kd-17298" && c.Repetitions == 0);
    }

    [Fact]
    public async Task Complete_UsesCamelCaseEnums()
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync(Url, new CompleteLessonRequest([], null, [], []));
        var json = await response.Content.ReadAsStringAsync();

        json.ShouldContain("\"status\":\"completed\"");
        json.ShouldContain("\"itemType\":\"vocab\"");
        json.ShouldContain("\"today\":\"2026-11-25\"");
    }

    [Fact]
    public async Task Complete_UnknownLesson_Returns404()
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync("/api/lessons/beginner/khong-co/complete", new CompleteLessonRequest([], null, [], []));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Complete_AnswerWithoutPayload_Returns400()
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync(Url,
            new CompleteLessonRequest([new SubmittedAnswerDto("nghia-gamsahamnida", null)], null, [], []));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Complete_TooManyAnswers_Returns400()
    {
        using var client = CreateClient();
        var answers = Enumerable.Range(0, CompleteLessonRequest.MaxAnswers + 1).Select(i => Choice($"x{i}", 0)).ToList();

        using var response = await client.PostAsJsonAsync(Url, new CompleteLessonRequest(answers, null, [], []));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
