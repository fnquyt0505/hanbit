using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hanbit.Web.Tests;

public class StyleguideTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Get_Styleguide_ListsAllColorTokens()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/styleguide");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        string[] tokens = ["--bg", "--surface", "--ink", "--ink-muted", "--border", "--primary",
            "--secondary", "--success", "--warning", "--danger", "--xp", "--success-ink", "--warning-ink", "--xp-ink"];
        foreach (var token in tokens)
        {
            html.ShouldContain($"data-token=\"{token}\"");
        }
    }
}
