using System.Net;
using System.Text.Json;

using Microsoft.AspNetCore.Mvc.Testing;

using Shouldly;

namespace Hanbit.Web.Tests;

public class StaticAssetsTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Theory]
    [InlineData("css/app.css")]
    [InlineData("lib/alpinejs/alpine.min.js")]
    public void EndpointsManifest_BuiltAsset_IsRegisteredForMapStaticAssets(string route)
    {
        // Production: MapStaticAssets chỉ phục vụ route có trong manifest này → thiếu là 404 trên Render.
        var manifestPath = Path.Combine(AppContext.BaseDirectory, "Hanbit.Web.staticwebassets.endpoints.json");
        using var manifest = JsonDocument.Parse(File.ReadAllText(manifestPath));

        var routes = manifest.RootElement.GetProperty("Endpoints")
            .EnumerateArray()
            .Select(e => e.GetProperty("Route").GetString());

        routes.ShouldContain(route);
    }

    [Fact]
    public async Task Get_TailwindCss_ReturnsCompiledUtilities()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/css/app.css");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/css");
        var css = await response.Content.ReadAsStringAsync();
        css.ShouldContain(".font-bold");
    }

    [Fact]
    public async Task Get_AlpineScript_ReturnsJavaScript()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/lib/alpinejs/alpine.min.js");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentType?.MediaType.ShouldBe("text/javascript");
    }
}
