using Microsoft.EntityFrameworkCore;

namespace Hanbit.Infrastructure.Persistence;

/// <summary>
/// Tiến độ của một tài khoản (giai đoạn 8 — T8.3 dùng). Lưu nguyên <c>LearnerProgress</c> dạng JSON (jsonb) — cùng cấu trúc
/// với localStorage/file sao lưu, nên gộp/khôi phục dùng lại <c>ProgressImporter</c>. Entity của tầng lưu trữ, không phải Domain.
/// </summary>
public sealed class LearnerProgressRecord
{
    /// <summary>Id tài khoản (ASP.NET Core Identity — T8.2).</summary>
    public required string UserId { get; set; }

    public int SchemaVersion { get; set; }

    /// <summary>JSON của <c>LearnerProgress</c> (camelCase như API).</summary>
    public required string Data { get; set; }

    public DateTimeOffset CreatedAt { get; set; }

    public DateTimeOffset UpdatedAt { get; set; }
}

/// <summary>
/// DbContext PostgreSQL (Neon). Tên bảng/cột snake_case đặt tay (không thêm package naming convention).
/// Đổi schema = thêm migration mới, không sửa migration đã apply (RULES).
/// </summary>
public sealed class HanbitDbContext(DbContextOptions<HanbitDbContext> options) : DbContext(options)
{
    public DbSet<LearnerProgressRecord> LearnerProgress => Set<LearnerProgressRecord>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<LearnerProgressRecord>(entity =>
        {
            entity.ToTable("learner_progress");
            entity.HasKey(e => e.UserId);
            entity.Property(e => e.UserId).HasColumnName("user_id").HasMaxLength(450);
            entity.Property(e => e.SchemaVersion).HasColumnName("schema_version");
            entity.Property(e => e.Data).HasColumnName("data").HasColumnType("jsonb");
            entity.Property(e => e.CreatedAt).HasColumnName("created_at");
            entity.Property(e => e.UpdatedAt).HasColumnName("updated_at");
        });
    }
}
