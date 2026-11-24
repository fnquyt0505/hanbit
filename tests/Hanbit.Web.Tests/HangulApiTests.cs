using System.Net;
using System.Net.Http.Json;

using Hanbit.Web.Controllers.Api;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>T3.3: integration test GET /api/hangul/compose.</summary>
public class HangulApiTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static string Url(string c, string v, string? f = null) =>
        $"/api/hangul/compose?c={Uri.EscapeDataString(c)}&v={Uri.EscapeDataString(v)}" +
        (f is null ? string.Empty : $"&f={Uri.EscapeDataString(f)}");

    [Theory]
    [InlineData("ㄱ", "ㅏ", null, "가", "ga")]
    [InlineData("ㅎ", "ㅏ", "ㄴ", "한", "han")]
    [InlineData("ㅂ", "ㅣ", "ㅊ", "빛", "bit")]
    public async Task Compose_Valid_ReturnsSyllable(string c, string v, string? f, string syllable, string romanization)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Url(c, v, f));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
        var body = await response.Content.ReadFromJsonAsync<ComposeResponse>();
        body.ShouldNotBeNull();
        body.Syllable.ShouldBe(syllable);
        body.Romanization.ShouldBe(romanization);
        body.Final.ShouldBe(f);
    }

    [Fact]
    public async Task Compose_UsesCamelCaseJson()
    {
        using var client = factory.CreateClient();

        var json = await client.GetStringAsync(Url("ㄱ", "ㅏ"));

        json.ShouldContain("\"syllable\":");
        json.ShouldContain("\"romanization\":\"ga\"");
    }

    [Theory]
    [InlineData("ㅏ", "ㅏ", null, "c")]
    [InlineData("ㄱ", "ㄱ", null, "v")]
    [InlineData("ㄱ", "ㅏ", "ㄸ", "f")]
    public async Task Compose_Invalid_Returns400ProblemWithFieldError(string c, string v, string? f, string field)
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync(Url(c, v, f));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("application/problem+json");
        var problem = await response.Content.ReadFromJsonAsync<ValidationProblemDetails>();
        problem.ShouldNotBeNull();
        problem.Errors.ShouldContainKey(field);
    }

    [Fact]
    public async Task Compose_MissingParameters_Returns400()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/api/hangul/compose");

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }
}
