using System.Security.Claims;

using Hanbit.Web.Hosting;

using Microsoft.AspNetCore.Mvc;

namespace Hanbit.Web.Controllers.Api;

/// <param name="Enabled">Site có bật tài khoản không (DB + Google OAuth).</param>
public sealed record AccountMeResponse(bool Enabled, bool SignedIn, string? DisplayName, string? Email);

/// <summary>Thông tin tài khoản hiện tại (T8.2) — JS dùng để quyết định đồng bộ tiến độ (T8.3).</summary>
[ApiController]
[Route("api/account")]
public sealed class AccountApiController(AccountFeature feature) : ControllerBase
{
    [HttpGet("me")]
    public AccountMeResponse Me()
    {
        var signedIn = feature.Enabled && User.Identity?.IsAuthenticated == true;
        return new AccountMeResponse(
            feature.Enabled,
            signedIn,
            signedIn ? User.FindFirstValue(AppUserClaimsPrincipalFactory.DisplayNameClaim) ?? User.Identity?.Name : null,
            signedIn ? User.FindFirstValue(ClaimTypes.Email) : null);
    }
}
