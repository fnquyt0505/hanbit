using System.Net;

using Hanbit.Application.Abstractions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hanbit.Web.Tests;

public class ContentWiringTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task ContentRepository_FromDi_ReadsRepositoryContentFolder()
    {
        var repository = factory.Services.GetRequiredService<IContentRepository>();

        var letters = await repository.GetHangulAsync(CancellationToken.None);

        letters.Count.ShouldBe(40);
    }

    [Fact]
    public async Task Get_Health_WhenContentFolderMissing_ReturnsUnhealthy()
    {
        using var brokenFactory = factory.WithWebHostBuilder(builder =>
            builder.UseSetting("Content:RootPath", "thu-muc-khong-ton-tai"));
        using var client = brokenFactory.CreateClient();

        using var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.ServiceUnavailable);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Unhealthy");
    }
}
