using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.RegularExpressions;

using Hanbit.Infrastructure.Persistence;
using Hanbit.Web.Controllers.Api;
using Hanbit.Web.Hosting;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Abstractions;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

using Npgsql;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>Chỉ chạy khi có <c>HANBIT_TEST_DB</c> (CI: container postgres) — xem Infrastructure.Tests.</summary>
public sealed class DatabaseFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "HANBIT_TEST_DB";

    public DatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)))
        {
            Skip = $"Chưa đặt {EnvironmentVariable} — bỏ qua test cần PostgreSQL.";
        }
    }
}

/// <summary>T8.2: tài khoản TẮT (thiếu DB hoặc Google OAuth) → chế độ khách như MVP.</summary>
public class AccountDisabledTests(WebApplicationFactory<Program> factory)
    : IClassFixture<WebApplicationFactory<Program>>
{
    [Fact]
    public async Task NoConfiguration_LoginIs404_NoLoginLink_MeSaysDisabled()
    {
        using var client = factory.CreateClient();

        (await client.GetAsync("/tai-khoan/dang-nhap")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        (await client.GetStringAsync("/")).ShouldNotContain("data-account=");
        var me = await client.GetFromJsonAsync<AccountMeResponse>("/api/account/me");
        me.ShouldBe(new AccountMeResponse(false, false, null, null));
    }

    [Fact]
    public async Task GoogleConfiguredButNoDatabase_StillDisabled()
    {
        using var configured = factory.WithWebHostBuilder(b => b
            .UseSetting("Authentication:Google:ClientId", "test-client")
            .UseSetting("Authentication:Google:ClientSecret", "test-secret"));
        using var client = configured.CreateClient();

        (await client.GetAsync("/tai-khoan/dang-nhap")).StatusCode.ShouldBe(HttpStatusCode.NotFound);
        configured.Services.GetRequiredService<AccountFeature>().Enabled.ShouldBeFalse();
    }
}

/// <summary>T8.2: filter chống CSRF — chỉ áp cho request GHI tới /api của người ĐÃ đăng nhập.</summary>
public class AuthenticatedApiAntiforgeryFilterTests
{
    private sealed class FakeAntiforgery(bool valid) : IAntiforgery
    {
        public AntiforgeryTokenSet GetAndStoreTokens(HttpContext httpContext) => throw new NotSupportedException();

        public AntiforgeryTokenSet GetTokens(HttpContext httpContext) => throw new NotSupportedException();

        public Task<bool> IsRequestValidAsync(HttpContext httpContext) => Task.FromResult(valid);

        public void SetCookieTokenAndHeader(HttpContext httpContext) => throw new NotSupportedException();

        public Task ValidateRequestAsync(HttpContext httpContext) => Task.CompletedTask;
    }

    private static HttpContext Context(string method, string path, bool authenticated)
    {
        var context = new DefaultHttpContext();
        context.Request.Method = method;
        context.Request.Path = path;
        context.User = authenticated
            ? new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.Name, "u")], "Identity.Application"))
            : new ClaimsPrincipal(new ClaimsIdentity());
        return context;
    }

    [Theory]
    [InlineData("POST", "/api/progress/summary", true, true)]
    [InlineData("DELETE", "/api/account", true, true)]
    [InlineData("GET", "/api/account/me", true, false)]
    [InlineData("POST", "/api/progress/summary", false, false)]
    [InlineData("POST", "/tai-khoan/dang-xuat", true, false)]
    public void RequiresValidation_OnlyAuthenticatedApiWrites(string method, string path, bool authenticated, bool expected)
    {
        AuthenticatedApiAntiforgeryFilter.RequiresValidation(Context(method, path, authenticated)).ShouldBe(expected);
    }

    [Theory]
    [InlineData(false, true)]
    [InlineData(true, false)]
    public async Task OnAuthorization_InvalidToken_Returns400(bool tokenValid, bool expectRejected)
    {
        var httpContext = Context("POST", "/api/progress/summary", authenticated: true);
        var filterContext = new AuthorizationFilterContext(
            new ActionContext(httpContext, new RouteData(), new ActionDescriptor()), []);

        await new AuthenticatedApiAntiforgeryFilter(new FakeAntiforgery(tokenValid)).OnAuthorizationAsync(filterContext);

        if (expectRejected)
        {
            var result = filterContext.Result.ShouldBeOfType<ObjectResult>();
            result.StatusCode.ShouldBe(StatusCodes.Status400BadRequest);
        }
        else
        {
            filterContext.Result.ShouldBeNull();
        }
    }
}

/// <summary>T8.2: tài khoản BẬT (DB + Google OAuth giả) — cần PostgreSQL (CI).</summary>
public sealed partial class AccountEnabledTests : IAsyncLifetime
{
    [GeneratedRegex("name=\"__RequestVerificationToken\" type=\"hidden\" value=\"([^\"]+)\"")]
    private static partial Regex TokenRegex();

    private WebApplicationFactory<Program>? _factory;
    private string? _connectionString;

    private WebApplicationFactory<Program> Factory => _factory ?? throw new InvalidOperationException("Chưa khởi tạo.");

    public async Task InitializeAsync()
    {
        var baseConnection = Environment.GetEnvironmentVariable(DatabaseFactAttribute.EnvironmentVariable);
        if (string.IsNullOrWhiteSpace(baseConnection))
        {
            return;
        }

        _connectionString = new NpgsqlConnectionStringBuilder(baseConnection) { Database = $"hanbit_acct_{Guid.NewGuid():N}" }.ConnectionString;

        // Migrate TRƯỚC khi khởi động host — nếu không, DatabaseMigrator (BackgroundService) chạy song song → "relation already exists".
        await using (var db = new HanbitDbContext(new DbContextOptionsBuilder<HanbitDbContext>().UseNpgsql(_connectionString).Options))
        {
            await db.Database.MigrateAsync();
        }

        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(b => b
            .UseSetting("ConnectionStrings:Hanbit", _connectionString)
            .UseSetting("Authentication:Google:ClientId", "test-client.apps.googleusercontent.com")
            .UseSetting("Authentication:Google:ClientSecret", "test-secret"));
    }

    public async Task DisposeAsync()
    {
        if (_factory is null)
        {
            return;
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            await scope.ServiceProvider.GetRequiredService<HanbitDbContext>().Database.EnsureDeletedAsync();
        }

        await _factory.DisposeAsync();
    }

    private HttpClient Client() => Factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });

    [DatabaseFact]
    public async Task LoginPage_ShowsGoogleButton_AndHeaderLink()
    {
        using var client = Client();

        var html = await client.GetStringAsync("/tai-khoan/dang-nhap?returnUrl=%2Freview");

        html.ShouldContain("data-testid=\"login-google\"");
        html.ShouldContain("name=\"returnUrl\" value=\"/review\"");
        html.ShouldContain("<meta name=\"robots\" content=\"noindex\" />");
        (await client.GetStringAsync("/hangul")).ShouldContain("data-account=\"signed-out\"");
        (await client.GetFromJsonAsync<AccountMeResponse>("/api/account/me")).ShouldBe(new AccountMeResponse(true, false, null, null));
    }

    [DatabaseFact]
    public async Task PostGoogle_RedirectsToGoogle_WithClientIdAndCallback_KeysPersisted()
    {
        using var client = Client();
        var html = await client.GetStringAsync("/tai-khoan/dang-nhap");
        var token = TokenRegex().Match(html).Groups[1].Value;
        token.ShouldNotBeNullOrEmpty();

        using var response = await client.PostAsync("/tai-khoan/google", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
            ["returnUrl"] = "/review",
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        var location = response.Headers.Location!.ToString();
        location.ShouldStartWith("https://accounts.google.com/o/oauth2/v2/auth");
        location.ShouldContain("client_id=test-client.apps.googleusercontent.com");
        location.ShouldContain(Uri.EscapeDataString("/signin-google"));

        await using var scope = Factory.Services.CreateAsyncScope();
        (await scope.ServiceProvider.GetRequiredService<HanbitDbContext>().DataProtectionKeys.CountAsync()).ShouldBeGreaterThan(0);
    }

    [DatabaseFact]
    public async Task PostGoogle_WithoutAntiforgeryToken_Returns400()
    {
        using var client = Client();

        using var response = await client.PostAsync("/tai-khoan/google", new FormUrlEncodedContent(new Dictionary<string, string>()));

        response.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [DatabaseFact]
    public async Task ExternalAccount_CreatesOnce_ThenFindsSameUser_UpdatesName()
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        var service = scope.ServiceProvider.GetRequiredService<ExternalAccountService>();
        var login = new UserLoginInfo("Google", "1234567890", "Google");

        var (first, error) = await service.FindOrCreateAsync(login, "hoc.vien@example.com", "Học Viên");
        var (second, _) = await service.FindOrCreateAsync(login, "hoc.vien@example.com", "Học Viên Mới");

        error.ShouldBeNull();
        first.ShouldNotBeNull();
        second!.Id.ShouldBe(first.Id);
        second.DisplayName.ShouldBe("Học Viên Mới");
        second.Email.ShouldBe("hoc.vien@example.com");
        second.EmailConfirmed.ShouldBeTrue();
        second.LastSignInAt.ShouldNotBeNull();
        (await scope.ServiceProvider.GetRequiredService<HanbitDbContext>().Users.CountAsync()).ShouldBe(1);
    }

    [DatabaseFact]
    public async Task Logout_WithToken_RedirectsHome()
    {
        using var client = Client();
        var html = await client.GetStringAsync("/tai-khoan/dang-nhap");
        var token = TokenRegex().Match(html).Groups[1].Value;

        using var response = await client.PostAsync("/tai-khoan/dang-xuat", new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["__RequestVerificationToken"] = token,
        }));

        response.StatusCode.ShouldBe(HttpStatusCode.Redirect);
        response.Headers.Location!.ToString().ShouldBe("/");
    }

    [DatabaseFact]
    public async Task OpenRedirect_ReturnUrlOutsideSite_FallsBackToDashboard()
    {
        using var client = Client();

        var html = await client.GetStringAsync("/tai-khoan/dang-nhap?returnUrl=https%3A%2F%2Fevil.example%2F");

        html.ShouldContain("name=\"returnUrl\" value=\"/dashboard\"");
    }
}
