using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>T4.4: /learn/{c}/{l} render từ vựng + ngữ pháp ở server, bài tập nhúng JSON không đáp án.</summary>
public class LessonPageTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private async Task<string> GetLessonHtmlAsync()
    {
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/learn/beginner/chao-hoi");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return await response.Content.ReadAsStringAsync();
    }

    [Fact]
    public async Task LessonPage_RendersVocabAndGrammarCards()
    {
        var html = await GetLessonHtmlAsync();

        html.Split("data-testid=\"vocab-card\"").Length.ShouldBe(15);
        html.Split("data-testid=\"grammar-card\"").Length.ShouldBe(2);
        html.ShouldContain("data-vocab-id=\"kd-17298\"");
        html.ShouldContain("https://krdict.korean.go.kr/");
        html.ShouldContain("/js/lesson-runner.js");
    }

    [Fact]
    public async Task LessonPage_EmbeddedJson_HasExercisesWithoutAnswers()
    {
        var html = await GetLessonHtmlAsync();

        var start = html.IndexOf("<script type=\"application/json\" id=\"lesson-data\">", StringComparison.Ordinal);
        start.ShouldBeGreaterThan(0);
        var json = html[start..html.IndexOf("</script>", start, StringComparison.Ordinal)];
        json.ShouldContain("\"exercises\":[");
        json.ShouldContain("nghia-gamsahamnida");
        foreach (var leak in new[] { "answerIndex", "answer_index", "acceptedAnswers", "accepted_answers", "explanation", "\"pairs\"" })
        {
            json.ShouldNotContain(leak, Case.Insensitive);
        }
    }

    [Fact]
    public async Task LessonPage_MarksLessonsNavItemCurrent_AndLinksBackToCourse()
    {
        var html = await GetLessonHtmlAsync();

        html.ShouldContain("href=\"/learn/beginner\" aria-current=\"page\"");
        html.ShouldContain("data-testid=\"lesson-exit\"");
    }

    [Theory]
    [InlineData("/learn/beginner/khong-co")]
    [InlineData("/learn/khong-co/chao-hoi")]
    public async Task UnknownLesson_Returns404(string path)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(path);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }
}
