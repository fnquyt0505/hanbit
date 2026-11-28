using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Hanbit.Infrastructure.Persistence;

public static class PersistenceServices
{
    /// <summary>Tên chuỗi kết nối trong cấu hình: <c>ConnectionStrings:Hanbit</c> (Render: biến môi trường <c>ConnectionStrings__Hanbit</c>).</summary>
    public const string ConnectionStringName = "Hanbit";

    /// <summary>
    /// Đăng ký PostgreSQL (Neon) — Web chỉ gọi khi có chuỗi kết nối; không có → app chạy chế độ khách như MVP.
    /// Thử lại khi lỗi tạm thời (Neon free tự tắt compute khi rảnh, lần đầu đánh thức có thể chậm).
    /// Migration được Web áp khi khởi động (<c>Hosting/DatabaseServices.cs</c>).
    /// </summary>
    public static IServiceCollection AddPersistence(this IServiceCollection services, string connectionString)
    {
        services.AddDbContext<HanbitDbContext>(options => options.UseNpgsql(connectionString, npgsql =>
            npgsql.EnableRetryOnFailure(maxRetryCount: 5, maxRetryDelay: TimeSpan.FromSeconds(5), errorCodesToAdd: null)));
        return services;
    }
}
