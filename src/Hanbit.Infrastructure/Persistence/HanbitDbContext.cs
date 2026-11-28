using Microsoft.AspNetCore.DataProtection.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace Hanbit.Infrastructure.Persistence;

/// <summary>Tài khoản (ASP.NET Core Identity — T8.2). Chỉ đăng nhập Google: không lưu mật khẩu.</summary>
public sealed class AppUser : IdentityUser
{
    /// <summary>Tên hiển thị lấy từ Google (có thể trống).</summary>
    [PersonalData]
    public string? DisplayName { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset? LastSignInAt { get; set; }
}

/// <summary>
/// Tiến độ của một tài khoản (T8.3 dùng). Lưu nguyên <c>LearnerProgress</c> dạng JSON (jsonb) — cùng cấu trúc
/// với localStorage/file sao lưu, nên gộp/khôi phục dùng lại <c>ProgressImporter</c>. Entity của tầng lưu trữ, không phải Domain.
/// </summary>
public sealed class LearnerProgressRecord
{
    /// <summary>Id tài khoản (<see cref="AppUser"/>).</summary>
    public required string UserId { get; set; }

    public int SchemaVersion { get; set; }

    /// <summary>JSON của <c>LearnerProgress</c> (camelCase như API).</summary>
    public required string Data { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// DbContext PostgreSQL (Neon): Identity (bảng AspNet* mặc định), khóa Data Protection (T8.2 — giữ phiên đăng nhập khi
/// Render khởi động lại container), tiến độ. Tên bảng của Hanbit snake_case đặt tay.
/// Đổi schema = thêm migration mới, không sửa migration đã apply (RULES).
/// </summary>
public sealed class HanbitDbContext(DbContextOptions<HanbitDbContext> options)
    : IdentityDbContext<AppUser>(options), IDataProtectionKeyContext
{
    public DbSet<LearnerProgressRecord> LearnerProgress => Set<LearnerProgressRecord>();

    public DbSet<DataProtectionKey> DataProtectionKeys => Set<DataProtectionKey>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.Entity<LearnerProgressRecord>(entity =>
        {
            entity.ToTable("learner_progress");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasColumnName("user_id").HasMaxLength(450);
            entity.Property(e => e.SchemaVersion).HasColumnName("schema_version");
            entity.Property(e => e.Data).HasColumnName("data").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
            // Xóa tài khoản → xóa tiến độ
            entity.HasOne<AppUser>().WithOne().HasForeignKey<LearnerProgressRecord>(e => e.UserId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<AppUser>(entity =>
        {
            entity.Property(e => e.DisplayName).HasMaxLength(200);
        });

        modelBuilder.Entity<DataProtectionKey>().ToTable("data_protection_keys");
    }
}
