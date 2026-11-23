using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Playwright;

namespace Hanbit.E2E;

/// <summary>Chạy Hanbit.Web trên Kestrel (cổng ngẫu nhiên) và một trình duyệt Chromium dùng chung.</summary>
public sealed class WebServerFixture : IAsyncLifetime
{
    private readonly WebApplicationFactory<Program> _factory = new();
    private IPlaywright? _playwright;
    private IBrowser? _browser;

    public string BaseUrl { get; private set; } = string.Empty;

    public IBrowser Browser => _browser ?? throw new InvalidOperationException("Fixture chưa khởi tạo.");

    public async Task InitializeAsync()
    {
        _factory.UseKestrel(0);
        _factory.StartServer();

        var addresses = _factory.Services.GetRequiredService<IServer>()
            .Features.GetRequiredFeature<IServerAddressesFeature>()
            .Addresses;
        BaseUrl = addresses.First();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync();
    }

    public async Task DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();
        await _factory.DisposeAsync();
    }
}

[CollectionDefinition(Name)]
public sealed class WebServerCollection : ICollectionFixture<WebServerFixture>
{
    public const string Name = "WebServer";
}
