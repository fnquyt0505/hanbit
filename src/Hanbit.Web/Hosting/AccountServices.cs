using Hanbit.Infrastructure.Persistence;

using Microsoft.AspNetCore.Antiforgery;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Hanbit.Web.Hosting;

/// <summary>Google OAuth (appsettings mục <c>Authentication:Google</c>; Render: <c>Authentication__Google__ClientId/ClientSecret</c>).</summary>
public sealed class GoogleAuthOptions
{
    public const string SectionName = "Authentication:Google";

    public string? ClientId { get; set; }

    public string? ClientSecret { get; set; }
}

/// <summary>Tài khoản có bật không (cần cả DB và Google OAuth) — view/controller dựa vào đây để ẩn/hiện đăng nhập.</summary>
public sealed record AccountFeature(bool Enabled);

/// <summary>
/// Giai đoạn 8 (T8.2): ASP.NET Core Identity (cookie) + đăng nhập Google. Chỉ bật khi có DB + Google OAuth;
/// thiếu → chế độ khách như MVP. Khóa Data Protection lưu vào DB để phiên đăng nhập không mất khi Render khởi động lại.
/// </summary>
public static class AccountServices
{
    public const string AntiforgeryHeader = "X-CSRF-TOKEN";
    public const string AuthCookieName = "hanbit.auth";
    public const string LoginPath = "/tai-khoan/dang-nhap";

    /// <returns>true nếu bật tài khoản.</returns>
    public static bool AddAccountsIfConfigured(this WebApplicationBuilder builder, bool databaseConfigured)
    {
        var services = builder.Services;
        var google = builder.Configuration.GetSection(GoogleAuthOptions.SectionName).Get<GoogleAuthOptions>() ?? new GoogleAuthOptions();
        var enabled = databaseConfigured && !string.IsNullOrWhiteSpace(google.ClientId) && !string.IsNullOrWhiteSpace(google.ClientSecret);
        services.AddSingleton(new AccountFeature(enabled));

        services.AddAntiforgery(options =>
        {
            options.HeaderName = AntiforgeryHeader;
            options.Cookie.Name = "hanbit.csrf";
            options.Cookie.SameSite = SameSiteMode.Strict;
        });

        if (databaseConfigured)
        {
            services.AddDataProtection().SetApplicationName("Hanbit").PersistKeysToDbContext<HanbitDbContext>();
        }

        if (!enabled)
        {
            return false;
        }

        services.AddAuthentication(options =>
            {
                options.DefaultScheme = IdentityConstants.ApplicationScheme;
                options.DefaultSignInScheme = IdentityConstants.ExternalScheme;
            })
            .AddGoogle(options =>
            {
                options.ClientId = google.ClientId!;
                options.ClientSecret = google.ClientSecret!;
                options.SignInScheme = IdentityConstants.ExternalScheme;
                options.CallbackPath = "/signin-google";
            })
            .AddIdentityCookies();

        services.AddIdentityCore<AppUser>(options => options.User.RequireUniqueEmail = false)
            .AddEntityFrameworkStores<HanbitDbContext>()
            .AddSignInManager()
            .AddClaimsPrincipalFactory<AppUserClaimsPrincipalFactory>();

        services.ConfigureApplicationCookie(options =>
        {
            options.Cookie.Name = AuthCookieName;
            options.Cookie.HttpOnly = true;
            options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Render: HTTPS (forwarded headers) → Secure
            options.Cookie.SameSite = SameSiteMode.Lax; // cần Lax để quay về từ Google
            options.ExpireTimeSpan = TimeSpan.FromDays(30);
            options.SlidingExpiration = true;
            options.LoginPath = LoginPath;
            options.AccessDeniedPath = LoginPath;
            options.Events.OnRedirectToLogin = context =>
            {
                // API: trả 401 thay vì chuyển hướng tới trang đăng nhập
                if (context.Request.Path.StartsWithSegments("/api"))
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                }

                context.Response.Redirect(context.RedirectUri);
                return Task.CompletedTask;
            };
        });

        services.AddScoped<ExternalAccountService>();
        services.Configure<MvcOptions>(options => options.Filters.Add<AuthenticatedApiAntiforgeryFilter>());
        return true;
    }
}

/// <summary>
/// Chống CSRF cho API dùng cookie đăng nhập: request ghi (POST/PUT/PATCH/DELETE) tới <c>/api/*</c> của người ĐÃ đăng nhập
/// phải có header <c>X-CSRF-TOKEN</c> hợp lệ. API ẩn danh (stateless, không đọc cookie) giữ nguyên.
/// </summary>
public sealed class AuthenticatedApiAntiforgeryFilter(IAntiforgery antiforgery) : IAsyncAuthorizationFilter
{
    public static bool RequiresValidation(HttpContext context) =>
        context.Request.Path.StartsWithSegments("/api")
        && !HttpMethods.IsGet(context.Request.Method)
        && !HttpMethods.IsHead(context.Request.Method)
        && !HttpMethods.IsOptions(context.Request.Method)
        && context.User.Identity?.IsAuthenticated == true;

    public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
    {
        if (!RequiresValidation(context.HttpContext))
        {
            return;
        }

        if (!await antiforgery.IsRequestValidAsync(context.HttpContext))
        {
            context.Result = new ObjectResult(new ProblemDetails
            {
                Status = StatusCodes.Status400BadRequest,
                Title = "Thiếu mã bảo vệ",
                Detail = "Yêu cầu thiếu mã chống giả mạo (CSRF). Tải lại trang rồi thử lại.",
            })
            { StatusCode = StatusCodes.Status400BadRequest };
        }
    }
}

/// <summary>Đăng nhập bằng Google: tìm tài khoản theo Google id, chưa có thì tạo (tên + email từ Google).</summary>
public sealed class ExternalAccountService(UserManager<AppUser> userManager, TimeProvider timeProvider)
{
    public const int MaxDisplayNameLength = 200;

    /// <returns>Tài khoản (đã có hoặc mới tạo), hoặc lỗi.</returns>
    public async Task<(AppUser? User, string? Error)> FindOrCreateAsync(UserLoginInfo login, string? email, string? name)
    {
        var now = timeProvider.GetUtcNow();
        var user = await userManager.FindByLoginAsync(login.LoginProvider, login.ProviderKey);
        if (user is null)
        {
            user = new AppUser
            {
                UserName = $"{login.LoginProvider.ToLowerInvariant()}-{login.ProviderKey}",
                Email = email,
                EmailConfirmed = !string.IsNullOrWhiteSpace(email), // Google đã xác minh email
                DisplayName = Truncate(name),
                CreatedAt = now,
            };
            var created = await userManager.CreateAsync(user);
            if (!created.Succeeded)
            {
                return (null, string.Join("; ", created.Errors.Select(e => e.Description)));
            }

            var linked = await userManager.AddLoginAsync(user, login);
            if (!linked.Succeeded)
            {
                return (null, string.Join("; ", linked.Errors.Select(e => e.Description)));
            }
        }

        user.LastSignInAt = now;
        if (!string.IsNullOrWhiteSpace(name))
        {
            user.DisplayName = Truncate(name);
        }

        await userManager.UpdateAsync(user);
        return (user, null);
    }

    private static string? Truncate(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim()[..Math.Min(value.Trim().Length, MaxDisplayNameLength)];
}

/// <summary>Thêm claim <c>display_name</c> (tên Google) vào cookie đăng nhập — header hiển thị không cần đọc DB.</summary>
public sealed class AppUserClaimsPrincipalFactory(UserManager<AppUser> userManager, Microsoft.Extensions.Options.IOptions<IdentityOptions> options)
    : UserClaimsPrincipalFactory<AppUser>(userManager, options)
{
    public const string DisplayNameClaim = "display_name";

    protected override async Task<System.Security.Claims.ClaimsIdentity> GenerateClaimsAsync(AppUser user)
    {
        var identity = await base.GenerateClaimsAsync(user);
        if (!string.IsNullOrWhiteSpace(user.DisplayName))
        {
            identity.AddClaim(new System.Security.Claims.Claim(DisplayNameClaim, user.DisplayName));
        }

        return identity;
    }
}
