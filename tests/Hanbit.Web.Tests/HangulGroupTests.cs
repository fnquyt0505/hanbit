using System.Net;
using System.Net.Http.Json;

using Hanbit.Web.Controllers.Api;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>T3.4: trang /hangul/{group} + API quiz (đề không chứa đáp án, chấm qua API).</summary>
public class HangulGroupTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("nguyen-am", 10)]
    [InlineData("phu-am", 14)]
    [InlineData("nguyen-am-ghep", 11)]
    [InlineData("phu-am-kep", 5)]
    public async Task GroupPage_RendersOneCardPerLetter(string group, int count)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync($"/hangul/{group}");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.Split("data-testid=\"letter-card\"").Length.ShouldBe(count + 1);
        html.ShouldContain("/js/hangul-quiz.js");
        html.ShouldContain("href=\"/hangul\" aria-current=\"page\"");
    }

    [Fact]
    public async Task GroupPage_UnknownGroup_Returns404Page()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/hangul/khong-co-nhom");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).ShouldContain("data-testid=\"status-page\"");
    }

    [Fact]
    public async Task HangulIndex_LinksToEveryGroup()
    {
        using var client = factory.CreateClient();

        var html = await client.GetStringAsync("/hangul");

        foreach (var group in new[] { "nguyen-am", "phu-am", "nguyen-am-ghep", "phu-am-kep" })
        {
            html.ShouldContain($"href=\"/hangul/{group}\"");
        }
    }

    [Fact]
    public async Task QuizApi_ReturnsTenQuestions_WithoutAnswers()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/hangul/quiz/phu-am-kep?seed=5");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var json = await response.Content.ReadAsStringAsync();
        json.ShouldNotContain("answer", Case.Insensitive);
        var quiz = await response.Content.ReadFromJsonAsync<QuizResponse>();
        quiz.ShouldNotBeNull();
        quiz.Questions.Count.ShouldBe(10);
        quiz.Questions.Select(q => q.Number).ShouldBe(Enumerable.Range(1, 10));
        quiz.Questions.ShouldAllBe(q => q.Choices.Count == 4);
        quiz.Questions.Select(q => q.Kind).Distinct().ShouldBe(
            ["char-to-romanization", "romanization-to-char", "listen-to-char"], ignoreOrder: true);
    }

    [Fact]
    public async Task QuizApi_UnknownGroup_Returns404()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/hangul/quiz/khong-co");

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData("char-to-romanization", "ㅏ", "a", true, "a")]
    [InlineData("char-to-romanization", "ㅏ", "eo", false, "a")]
    [InlineData("romanization-to-char", "yo", "ㅛ", true, "ㅛ")]
    [InlineData("listen-to-char", "아", "ㅓ", false, "ㅏ")]
    public async Task CheckApi_GradesAnswer(string kind, string prompt, string answer, bool correct, string correctAnswer)
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/hangul/quiz/check",
            new QuizCheckRequest("nguyen-am", kind, prompt, answer));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<QuizCheckResponse>();
        body.ShouldNotBeNull();
        body.Correct.ShouldBe(correct);
        body.CorrectAnswer.ShouldBe(correctAnswer);
    }

    [Theory]
    [InlineData("khong-co", "char-to-romanization", "ㅏ")]
    [InlineData("nguyen-am", "doan-bua", "ㅏ")]
    [InlineData("nguyen-am", "char-to-romanization", "ㄱ")]
    public async Task CheckApi_InvalidRequest_Returns400(string group, string kind, string prompt)
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/hangul/quiz/check",
            new QuizCheckRequest(group, kind, prompt, "a"));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task QuizApi_EveryQuestion_IsGradableByCheckApi()
    {
        using var client = factory.CreateClient();
        var quiz = await client.GetFromJsonAsync<QuizResponse>("/api/hangul/quiz/phu-am?seed=11");
        quiz.ShouldNotBeNull();

        foreach (var q in quiz.Questions)
        {
            using var response = await client.PostAsJsonAsync("/api/hangul/quiz/check",
                new QuizCheckRequest("phu-am", q.Kind, q.Prompt, q.Choices[0]));
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            var check = await response.Content.ReadFromJsonAsync<QuizCheckResponse>();
            q.Choices.ShouldContain(check!.CorrectAnswer);
        }
    }
}
