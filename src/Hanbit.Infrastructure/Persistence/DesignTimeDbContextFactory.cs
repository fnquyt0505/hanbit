using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace Hanbit.Infrastructure.Persistence;

/// <summary>
/// Cho <c>dotnet ef migrations add</c> (không cần kết nối DB thật):
/// <c>dotnet ef migrations add &lt;Tên&gt; -p src/Hanbit.Infrastructure -s src/Hanbit.Infrastructure -o Persistence/Migrations</c>.
/// Áp migration lên DB thật: app tự chạy khi khởi động (<c>DatabaseMigrator</c>).
/// </summary>
public sealed class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HanbitDbContext>
{
    public HanbitDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("ConnectionStrings__Hanbit")
            ?? "Host=localhost;Database=hanbit_design;Username=postgres;Password=postgres";
        return new HanbitDbContext(new DbContextOptionsBuilder<HanbitDbContext>().UseNpgsql(connectionString).Options);
    }
}
