using System.IO.Compression;
using System.Net;

using Hanbit.Application.Abstractions;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>T7.6: nén HTML/JSON động + nạp sẵn nội dung khi khởi động.</summary>
public class PerformanceTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    private static HttpRequestMessage Get(string path, string encoding)
    {
        var request = new HttpRequestMessage(HttpMethod.Get, path);
        request.Headers.AcceptEncoding.ParseAdd(encoding);
        return request;
    }

    [Theory]
    [InlineData("/hangul", "br")]
    [InlineData("/learn/beginner/chao-hoi", "gzip")]
    [InlineData("/api/lessons/beginner/chao-hoi", "br")]
    public async Task DynamicResponses_AreCompressed_AndMuchSmaller(string path, string encoding)
    {
        using var client = factory.CreateClient();
        using var plain = await client.GetAsync(path);
        var plainBytes = await plain.Content.ReadAsByteArrayAsync();

        using var response = await client.SendAsync(Get(path, encoding));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        response.Content.Headers.ContentEncoding.ShouldBe([encoding]);
        var compressed = await response.Content.ReadAsByteArrayAsync();
        compressed.Length.ShouldBeLessThan(plainBytes.Length / 2);

        await using var stream = encoding == "br"
            ? (Stream)new BrotliStream(new MemoryStream(compressed), CompressionMode.Decompress)
            : new GZipStream(new MemoryStream(compressed), CompressionMode.Decompress);
        using var reader = new StreamReader(stream);
        (await reader.ReadToEndAsync()).Length.ShouldBe(System.Text.Encoding.UTF8.GetString(plainBytes).Length);
    }

    [Fact]
    public async Task NoAcceptEncoding_NotCompressed()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/hangul");

        response.Content.Headers.ContentEncoding.ShouldBeEmpty();
    }

    [Fact]
    public void ContentWarmup_IsRegisteredAsHostedService()
    {
        factory.Services.GetServices<IHostedService>().ShouldContain(s => s.GetType().Name == "ContentWarmup");
    }

    [Fact]
    public async Task ContentWarmup_RunsWithoutError()
    {
        var warmup = factory.Services.GetServices<IHostedService>().Single(s => s.GetType().Name == "ContentWarmup");
        var repository = factory.Services.GetRequiredService<IContentRepository>();

        await warmup.StartAsync(CancellationToken.None);
        await ((BackgroundService)warmup).ExecuteTask!;

        (await repository.GetCoursesAsync(CancellationToken.None)).ShouldNotBeEmpty();
    }
}
