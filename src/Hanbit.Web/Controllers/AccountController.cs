using System.Security.Claims;

using Hanbit.Infrastructure.Persistence;
using Hanbit.Web.Hosting;

using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers;

/// <summary>Đăng nhập / đăng xuất bằng Google (T8.2). Tài khoản chưa bật (thiếu DB hoặc Google OAuth) → 404.</summary>
[Route("tai-khoan")]
public sealed class AccountController(AccountFeature feature, IServiceProvider services, ILogger<AccountController> logger) : Controller
{
    private SignInManager<AppUser> SignInManager => services.GetRequiredService<SignInManager<AppUser>>();

    [HttpGet("dang-nhap")]
    public IActionResult Login(string? returnUrl = null, string? error = null)
    {
        if (!feature.Enabled)
        {
            return NotFound();
        }

        if (User.Identity?.IsAuthenticated == true)
        {
            return LocalRedirect(SafeReturnUrl(returnUrl));
        }

        ViewData["ReturnUrl"] = SafeReturnUrl(returnUrl);
        ViewData["Error"] = error is null ? null : "Không đăng nhập được bằng Google. Bạn thử lại nhé.";
        return View();
    }

    /// <summary>Bắt đầu đăng nhập Google (form POST có mã chống giả mạo).</summary>
    [HttpPost("google")]
    [ValidateAntiForgeryToken]
    public IActionResult Google(string? returnUrl = null)
    {
        if (!feature.Enabled)
        {
            return NotFound();
        }

        var redirect = Url.Action(nameof(GoogleCallback), new { returnUrl = SafeReturnUrl(returnUrl) });
        var properties = SignInManager.ConfigureExternalAuthenticationProperties(GoogleDefaults.AuthenticationScheme, redirect);
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    [HttpGet("google-callback")]
    public async Task<IActionResult> GoogleCallback(string? returnUrl = null, string? remoteError = null)
    {
        if (!feature.Enabled)
        {
            return NotFound();
        }

        var info = remoteError is null ? await SignInManager.GetExternalLoginInfoAsync() : null;
        if (info is null)
        {
            logger.LogWarning("Đăng nhập Google thất bại: {Error}", remoteError ?? "không có thông tin đăng nhập ngoài");
            return RedirectToAction(nameof(Login), new { returnUrl, error = "google" });
        }

        var accounts = services.GetRequiredService<ExternalAccountService>();
        var (user, createError) = await accounts.FindOrCreateAsync(
            new UserLoginInfo(info.LoginProvider, info.ProviderKey, info.ProviderDisplayName),
            info.Principal.FindFirstValue(ClaimTypes.Email),
            info.Principal.FindFirstValue(ClaimTypes.Name));
        if (user is null)
        {
            logger.LogError("Không tạo được tài khoản từ Google: {Error}", createError);
            return RedirectToAction(nameof(Login), new { returnUrl, error = "account" });
        }

        await SignInManager.SignInAsync(user, isPersistent: true, authenticationMethod: info.LoginProvider);
        return LocalRedirect(SafeReturnUrl(returnUrl));
    }

    [HttpPost("dang-xuat")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        if (feature.Enabled)
        {
            await SignInManager.SignOutAsync();
        }

        return LocalRedirect("/");
    }

    /// <summary>Chỉ cho chuyển hướng trong site (chống open redirect); mặc định về tổng quan.</summary>
    private string SafeReturnUrl(string? returnUrl) =>
        !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl) ? returnUrl : "/dashboard";
}
