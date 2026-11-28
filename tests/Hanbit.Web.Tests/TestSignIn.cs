using Hanbit.Infrastructure.Persistence;

using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;

using Shouldly;

namespace Hanbit.Web.Tests;

/// <summary>Đăng nhập không qua Google: tạo tài khoản theo tên rồi phát cookie như sau callback.</summary>
internal sealed class TestSignInStartupFilter : IStartupFilter
{
    public const string Path = "/__test/sign-in";

    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        // Không dùng app.Map: Map đặt PathBase = /__test/sign-in → cookie đăng nhập bị gắn Path đó, trang khác không nhận.
        app.Use(async (context, nextMiddleware) =>
        {
            if (!context.Request.Path.Equals(Path, StringComparison.Ordinal))
            {
                await nextMiddleware(context);
                return;
            }

            var name = context.Request.Query["name"].ToString();
            var users = context.RequestServices.GetRequiredService<UserManager<AppUser>>();
            var user = await users.FindByNameAsync(name);
            if (user is null)
            {
                user = new AppUser { UserName = name, CreatedAt = DateTimeOffset.UtcNow };
                (await users.CreateAsync(user)).Succeeded.ShouldBeTrue();
            }

            await context.RequestServices.GetRequiredService<SignInManager<AppUser>>().SignInAsync(user, isPersistent: true);
            context.Response.StatusCode = StatusCodes.Status204NoContent;
        });
        next(app);
    };
}
