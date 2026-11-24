using System.Net;
using System.Net.Http.Json;

using Hanbit.Domain.Content;
using Hanbit.Web.Models.Learn;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>T4.3: GET /api/lessons/{c}/{l} (không lộ đáp án) + POST /api/exercises/check.</summary>
public class LessonsApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private const string Lesson = "/api/lessons/beginner/chao-hoi";

    private static CheckExerciseRequest Request(string exerciseId, AnswerDto answer) =>
        new("beginner", "chao-hoi", exerciseId, answer);

    private static AnswerDto Index(int i) => new(i, null, null, null);

    private async Task<CheckExerciseResponse> CheckAsync(HttpClient client, CheckExerciseRequest request)
    {
        using var response = await client.PostAsJsonAsync("/api/exercises/check", request);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<CheckExerciseResponse>())!;
    }

    [Fact]
    public async Task GetLesson_ReturnsVocabGrammarAndAuthoredPlusGeneratedExercises()
    {
        using var client = factory.CreateClient();

        var lesson = await client.GetFromJsonAsync<LessonDto>(Lesson);

        lesson.ShouldNotBeNull();
        lesson.Title.ShouldBe("Chào hỏi");
        lesson.NextSlug.ShouldBe("gioi-thieu");
        lesson.Vocab.Count.ShouldBe(14);
        lesson.Vocab[0].Ko.ShouldBe("안녕");
        lesson.Vocab[0].Romanization.ShouldBe("annyeong");
        lesson.Vocab.ShouldAllBe(v => v.SourceUrl.StartsWith("https://krdict.korean.go.kr"));
        lesson.Grammar.ShouldHaveSingleItem().Id.ShouldBe("ending-mnida-seumnida");
        lesson.Exercises.Count.ShouldBe(14);
        lesson.Exercises.Count(e => e.Id.StartsWith("gen-")).ShouldBe(7);
    }

    [Fact]
    public async Task GetLesson_JsonContainsNoAnswers()
    {
        using var client = factory.CreateClient();

        var full = await client.GetStringAsync(Lesson);
        var json = full[full.IndexOf("\"exercises\"", StringComparison.Ordinal)..]; // ngữ pháp có explanationVi hợp lệ

        // Tên thuộc tính chứa đáp án (camelCase lẫn snake_case) — "type_answer" là tên loại, không phải đáp án.
        foreach (var leak in new[] { "\"answerIndex\"", "\"answer_index\"", "\"acceptedAnswers\"", "\"accepted_answers\"", "\"explanationVi\"", "\"explanation_vi\"", "\"pairs\"" })
        {
            json.ShouldNotContain(leak, Case.Insensitive);
        }
    }

    [Fact]
    public async Task GetLesson_ReorderTokensAreShuffled_MatchColumnsSplit()
    {
        using var client = factory.CreateClient();

        var lesson = await client.GetFromJsonAsync<LessonDto>(Lesson);

        var reorder = lesson!.Exercises.Single(e => e.Type == "reorder");
        reorder.Tokens.ShouldNotBeNull();
        reorder.Tokens.ShouldBe(["처음", "뵙겠습니다.", "반갑습니다."], ignoreOrder: true);
        reorder.Tokens.ShouldNotBe(["처음", "뵙겠습니다.", "반갑습니다."]);

        var match = lesson.Exercises.First(e => e.Type == "match_pairs");
        match.Left!.Count.ShouldBe(match.Right!.Count);
    }

    [Theory]
    [InlineData("/api/lessons/beginner/khong-co")]
    [InlineData("/api/lessons/khong-co/chao-hoi")]
    [InlineData("/api/lessons/beginner/..%2Fcourse")]
    public async Task GetLesson_Unknown_Returns404(string url)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(url);

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task Check_AuthoredMultipleChoice()
    {
        using var client = factory.CreateClient();

        var right = await CheckAsync(client, Request("nghia-gamsahamnida", Index(1)));
        var wrong = await CheckAsync(client, Request("nghia-gamsahamnida", Index(0)));

        right.Correct.ShouldBeTrue();
        wrong.Correct.ShouldBeFalse();
        wrong.CorrectAnswer.ShouldBe("Cảm ơn");
    }

    [Fact]
    public async Task Check_TypeAnswer_ReorderAndMatchPairs()
    {
        using var client = factory.CreateClient();

        (await CheckAsync(client, Request("go-bangapseumnida", new(null, null, null, " 반갑습니다! ")))).Correct.ShouldBeTrue();
        (await CheckAsync(client, Request("sap-xep-gap-lan-dau", new(null, null, ["처음", "뵙겠습니다.", "반갑습니다."], null)))).Correct.ShouldBeTrue();
        (await CheckAsync(client, Request("sap-xep-gap-lan-dau", new(null, null, ["반갑습니다.", "처음", "뵙겠습니다."], null)))).Correct.ShouldBeFalse();

        var pairs = new List<MatchPair> { new("인사", "sự chào hỏi"), new("축하", "sự chúc mừng"), new("환영", "sự hoan nghênh"), new("감사", "sự cảm tạ") };
        (await CheckAsync(client, Request("noi-tu-chao-hoi", new(null, pairs, null, null)))).Correct.ShouldBeTrue();
    }

    [Fact]
    public async Task Check_GeneratedChoiceExercises_ExactlyOneChoiceIsCorrect()
    {
        using var client = factory.CreateClient();
        var lesson = await client.GetFromJsonAsync<LessonDto>(Lesson);

        foreach (var exercise in lesson!.Exercises.Where(e => e.Id.StartsWith("gen-") && e.Choices is not null))
        {
            var correctCount = 0;
            for (var i = 0; i < exercise.Choices!.Count; i++)
            {
                if ((await CheckAsync(client, Request(exercise.Id, Index(i)))).Correct)
                {
                    correctCount++;
                }
            }

            correctCount.ShouldBe(1, exercise.Id);
        }
    }

    [Fact]
    public async Task Check_UnknownExercise_Returns404()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/exercises/check", Request("khong-co", Index(0)));

        response.StatusCode.ShouldBe(HttpStatusCode.NotFound);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("text")]
    public async Task Check_MissingOrMismatchedAnswer_Returns400(string? kind)
    {
        using var client = factory.CreateClient();
        var answer = kind is null ? new AnswerDto(null, null, null, null) : new AnswerDto(null, null, null, "Cảm ơn");

        using var response = await client.PostAsJsonAsync("/api/exercises/check", Request("nghia-gamsahamnida", answer));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
    }

    [Fact]
    public async Task Check_MissingExerciseRef_Returns400()
    {
        using var client = factory.CreateClient();

        using var response = await client.PostAsJsonAsync("/api/exercises/check", new CheckExerciseRequest(null, "chao-hoi", "x", Index(0)));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
