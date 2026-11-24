using Hanbit.Application.Abstractions;
using Hanbit.Infrastructure.Content;

using Microsoft.Extensions.DependencyInjection;

namespace Hanbit.Infrastructure;

public static class DependencyInjection
{
    /// <summary>
    /// Đăng ký dịch vụ Infrastructure. <see cref="ContentOptions"/> do Web cấu hình (bind từ appsettings mục <c>Content</c>).
    /// </summary>
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddMemoryCache();
        services.AddOptions<ContentOptions>();
        services.AddSingleton<IContentRepository, JsonContentRepository>();
        services.AddSingleton(TimeProvider.System);
        services.AddSingleton<IClock, SystemClock>();
        return services;
    }
}
