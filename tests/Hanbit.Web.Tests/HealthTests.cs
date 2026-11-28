using System.Net;

using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hanbit.Web.Tests;

public class HealthTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task Get_Health_ReturnsHealthy()
    {
        using var client = factory.CreateClient();

        using var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe("Healthy");
    }

    [Fact]
    public async Task Get_HealthOverHttp_IsNotRedirected()
    {
        // Health check của Render gọi HTTP nội bộ → không được redirect sang HTTPS.
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("http://localhost"),
            AllowAutoRedirect = false,
        });

        using var response = await client.GetAsync("/health");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }
}

/// <summary>T8.1: PostgreSQL tùy chọn — không cấu hình thì chạy chế độ khách; DB lỗi → /health Degraded, trang vẫn chạy.</summary>
public class DatabaseWiringTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public void NoConnectionString_DbContextNotRegistered()
    {
        using var scope = factory.Services.CreateScope();

        scope.ServiceProvider.GetService<Hanbit.Infrastructure.Persistence.HanbitDbContext>().ShouldBeNull();
    }

    [Fact]
    public async Task UnreachableDatabase_HealthDegraded_PagesStillWork()
    {
        using var broken = factory.WithWebHostBuilder(b => b.UseSetting(
            "ConnectionStrings:Hanbit", "Host=127.0.0.1;Port=1;Database=x;Username=x;Password=x;Timeout=1;Command Timeout=1"));
        using var client = broken.CreateClient();

        using var health = await client.GetAsync("/health");
        using var page = await client.GetAsync("/hangul");

        health.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await health.Content.ReadAsStringAsync()).ShouldBe("Degraded");
        page.StatusCode.ShouldBe(HttpStatusCode.OK);
        using var scope = broken.Services.CreateScope();
        scope.ServiceProvider.GetService<Hanbit.Infrastructure.Persistence.HanbitDbContext>().ShouldNotBeNull();
    }
}
