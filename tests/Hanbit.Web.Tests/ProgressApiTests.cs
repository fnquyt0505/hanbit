using System.Net;
using System.Net.Http.Json;

using Hanbit.Application.Abstractions;
using Hanbit.Domain.Progress;
using Hanbit.Web.Controllers.Api;
using Hanbit.Web.Models.Progress;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>T5.4: POST /api/progress/summary + POST /api/hangul/quiz/complete.</summary>
public class ProgressApiTests(WebApplicationFactory<Program> factory)
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

    private static async Task<T> ReadAsync<T>(HttpResponseMessage response)
    {
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<T>(TestJson.Options))!;
    }

    [Fact]
    public async Task Summary_NewLearner_SuggestsFirstLesson()
    {
        using var client = CreateClient();

        var summary = await ReadAsync<ProgressSummaryResponse>(
            await client.PostAsJsonAsync("/api/progress/summary", new ProgressSummaryRequest(null, null, null, 0)));

        summary.IsNewLearner.ShouldBeTrue();
        summary.Streak.ShouldBe(new Streak(0, 0, false));
        summary.NextLesson.ShouldNotBeNull();
        summary.NextLesson.Href.ShouldBe("/learn/beginner/chao-hoi");
        summary.Today.ShouldBe(Today);
    }

    [Fact]
    public async Task Summary_WithProgress_ComputesStreakDueCardsAndNextLesson()
    {
        using var client = CreateClient();
        var request = new ProgressSummaryRequest(
            new Dictionary<string, LessonRecord> { ["beginner/chao-hoi"] = new(LessonStatus.Completed, 70, Now) },
            [new ReviewCard("kd-1", ItemType.Vocab, 2.5, 0, 0, Now.AddHours(-1), null), new ReviewCard("kd-2", ItemType.Vocab, 2.5, 0, 0, Now.AddDays(1), null)],
            [new DailyActivity(Today.AddDays(-1), 10, 0), new DailyActivity(Today, 24, 0)],
            34);

        var summary = await ReadAsync<ProgressSummaryResponse>(await client.PostAsJsonAsync("/api/progress/summary", request));

        summary.IsNewLearner.ShouldBeFalse();
        summary.Streak.Current.ShouldBe(2);
        summary.XpToday.ShouldBe(24);
        summary.TotalXp.ShouldBe(34);
        summary.DueCards.ShouldBe(1);
        summary.LearnedWords.ShouldBe(2);
        summary.CompletedLessons.ShouldBe(1);
        summary.NextLesson!.Slug.ShouldBe("gioi-thieu");
    }

    [Fact]
    public async Task HangulComplete_AllCorrect_FifteenXp_KeyedByGroup()
    {
        using var client = CreateClient();
        var quiz = await client.GetFromJsonAsync<QuizResponse>("/api/hangul/quiz/nguyen-am?seed=3");
        var answers = new List<HangulQuizAnswerDto>();
        foreach (var q in quiz!.Questions)
        {
            var check = await ReadAsync<QuizCheckResponse>(await client.PostAsJsonAsync("/api/hangul/quiz/check",
                new QuizCheckRequest("nguyen-am", q.Kind, q.Prompt, q.Choices[0])));
            answers.Add(new HangulQuizAnswerDto(q.Kind, q.Prompt, check.CorrectAnswer));
        }

        var result = await ReadAsync<CompleteLessonResponse>(await client.PostAsJsonAsync("/api/hangul/quiz/complete",
            new HangulQuizCompleteRequest("nguyen-am", answers, null, [])));

        result.LessonKey.ShouldBe("hangul/nguyen-am");
        result.Correct.ShouldBe(10);
        result.Xp.ShouldBe(15);
        result.Score.ShouldBe(100);
        result.NewCards.ShouldBeEmpty();
        result.Streak.Current.ShouldBe(1);
    }

    [Theory]
    [InlineData("khong-co", "char-to-romanization", "ㅏ")]
    [InlineData("nguyen-am", "doan", "ㅏ")]
    [InlineData("nguyen-am", "char-to-romanization", "ㄱ")]
    public async Task HangulComplete_InvalidRequest_Returns400(string group, string kind, string prompt)
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync("/api/hangul/quiz/complete",
            new HangulQuizCompleteRequest(group, [new HangulQuizAnswerDto(kind, prompt, "a")], null, []));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task HangulComplete_NoAnswers_Returns400()
    {
        using var client = CreateClient();

        using var response = await client.PostAsJsonAsync("/api/hangul/quiz/complete",
            new HangulQuizCompleteRequest("nguyen-am", [], null, []));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}

/// <summary>T7.1: POST /api/progress/stats.</summary>
public class ProgressStatsApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Stats_ReturnsWordStatusesCalendarAndStreak()
    {
        using var client = factory.CreateClient();
        var now = DateTimeOffset.UtcNow;
        var today = Hanbit.Domain.Progress.VietnamTime.ToDate(now);
        var request = new ProgressStatsRequest(
            [
                new ReviewCard("kd-44487", ItemType.Vocab, 2.6, 30, 5, now.AddDays(30), now),  // 가족 — đã thuộc
                new ReviewCard("kd-17298", ItemType.Vocab, 2.5, 3, 2, now.AddDays(3), now),   // 안녕 — đang học
                new ReviewCard("kd-1", ItemType.Vocab, 2.5, 3, 2, now.AddDays(3), now),        // không có trong nội dung
            ],
            [new DailyActivity(today, 30, 5)]);

        using var response = await client.PostAsJsonAsync("/api/progress/stats", request, TestJson.Options);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stats = (await response.Content.ReadFromJsonAsync<ProgressStatsResponse>(TestJson.Options))!;
        stats.MasteredWords.ShouldBe(1);
        stats.LearningWords.ShouldBe(2);
        stats.TotalReviews.ShouldBe(5);
        stats.Streak.Current.ShouldBe(1);
        stats.Calendar[^1].Date.ShouldBe(today);
        stats.Words.Select(w => (w.Ko, w.Status)).ShouldBe([("가족", WordStatus.Mastered), ("안녕", WordStatus.Learning)]);

        (await response.Content.ReadAsStringAsync()).ShouldContain("\"status\":\"mastered\"");
    }
}
