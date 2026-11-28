using Hanbit.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Hanbit.Web.Hosting;

/// <summary>Giai đoạn 8 (T8.1): PostgreSQL TÙY CHỌN — chỉ bật khi có <c>ConnectionStrings:Hanbit</c>.</summary>
public static class DatabaseServices
{
    /// <returns>true nếu đã cấu hình DB.</returns>
    public static bool AddDatabaseIfConfigured(this WebApplicationBuilder builder)
    {
        var connectionString = builder.Configuration.GetConnectionString(PersistenceServices.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        builder.Services.AddPersistence(connectionString);
        builder.Services.AddHostedService<DatabaseMigrator>();
        builder.Services.AddHealthChecks().AddCheck<DatabaseHealthCheck>("database", failureStatus: HealthStatus.Degraded);
        return true;
    }
}

/// <summary>
/// Áp migration khi khởi động (một instance trên Render — đủ đơn giản). Lỗi chỉ ghi log: chế độ khách không cần DB.
/// </summary>
public sealed class DatabaseMigrator(IServiceScopeFactory scopeFactory, ILogger<DatabaseMigrator> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<HanbitDbContext>();
            var pending = (await db.Database.GetPendingMigrationsAsync(stoppingToken)).ToList();
            if (pending.Count > 0)
            {
                await db.Database.MigrateAsync(stoppingToken);
                logger.LogInformation("Đã áp {Count} migration: {Migrations}.", pending.Count, string.Join(", ", pending));
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // đang tắt
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Không áp được migration — tính năng tài khoản tạm thời không dùng được, chế độ khách vẫn chạy.");
        }
    }
}

/// <summary>
/// /health: kiểm tra kết nối DB. Lỗi → Degraded (vẫn HTTP 200) để Render không khởi động lại app — chế độ khách không phụ thuộc DB.
/// </summary>
public sealed class DatabaseHealthCheck(HanbitDbContext db) : IHealthCheck
{
    /// <summary>Giới hạn thời gian — chiến lược thử lại của Npgsql có thể kéo dài ~25 s, quá ngưỡng health check của Render.</summary>
    public static readonly TimeSpan Timeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(Timeout);
        try
        {
            return await db.Database.CanConnectAsync(timeout.Token)
                ? HealthCheckResult.Healthy("Kết nối PostgreSQL OK.")
                : HealthCheckResult.Degraded("Không kết nối được PostgreSQL.");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Degraded("Không kết nối được PostgreSQL.", ex);
        }
    }
}
