using Hanbit.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Npgsql;

using Shouldly;

namespace Hanbit.Infrastructure.Tests.Persistence;

/// <summary>
/// Chỉ chạy khi có biến môi trường <c>HANBIT_TEST_DB</c> (chuỗi kết nối tới một PostgreSQL thử nghiệm — CI dùng container
/// postgres; máy dev có thể dùng một branch Neon riêng). Không có → bỏ qua (Skip).
/// </summary>
public sealed class DatabaseFactAttribute : FactAttribute
{
    public const string EnvironmentVariable = "HANBIT_TEST_DB";

    public DatabaseFactAttribute()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(EnvironmentVariable)))
        {
            Skip = $"Chưa đặt {EnvironmentVariable} — bỏ qua test cần PostgreSQL.";
        }
    }
}

/// <summary>T8.1: model EF Core + migration Initial (không cần DB).</summary>
public class PersistenceModelTests
{
    private static HanbitDbContext CreateContext() =>
        new(new DbContextOptionsBuilder<HanbitDbContext>().UseNpgsql("Host=localhost;Database=unused").Options);

    [Fact]
    public void Model_HasNoChangesWithoutMigration()
    {
        using var db = CreateContext();

        db.Database.HasPendingModelChanges().ShouldBeFalse("Model đổi nhưng chưa có migration — chạy dotnet ef migrations add.");
    }

    [Fact]
    public void Migrations_StartWithInitial()
    {
        using var db = CreateContext();

        db.Database.GetMigrations().First().ShouldEndWith("_Initial");
    }

    [Fact]
    public void LearnerProgress_MapsToSnakeCaseJsonbTable()
    {
        using var db = CreateContext();
        var entity = db.Model.FindEntityType(typeof(LearnerProgressRecord))!;

        entity.GetTableName().ShouldBe("learner_progress");
        entity.FindPrimaryKey()!.Properties.Single().GetColumnName().ShouldBe("user_id");
        entity.FindProperty(nameof(LearnerProgressRecord.Data))!.GetColumnType().ShouldBe("jsonb");
    }
}

/// <summary>T8.1: áp migration lên PostgreSQL thật + đọc/ghi jsonb.</summary>
public class PersistenceDatabaseTests
{
    [DatabaseFact]
    public async Task Migrate_ThenRoundTripJsonbProgress()
    {
        var baseConnection = new NpgsqlConnectionStringBuilder(Environment.GetEnvironmentVariable(DatabaseFactAttribute.EnvironmentVariable))
        {
            Database = $"hanbit_test_{Guid.NewGuid():N}",
        };
        var options = new DbContextOptionsBuilder<HanbitDbContext>().UseNpgsql(baseConnection.ConnectionString).Options;

        await using var db = new HanbitDbContext(options);
        try
        {
            await db.Database.MigrateAsync();
            (await db.Database.GetPendingMigrationsAsync()).ShouldBeEmpty();

            var now = new DateTimeOffset(2026, 11, 28, 10, 0, 0, TimeSpan.Zero);
            db.Users.Add(new AppUser { Id = "user-1", UserName = "google-1", CreatedAt = now }); // FK learner_progress → tài khoản (T8.2)
            db.LearnerProgress.Add(new LearnerProgressRecord
            {
                UserId = "user-1",
                SchemaVersion = 1,
                Data = """{"schemaVersion":1,"totalXp":42,"lessons":{"beginner/chao-hoi":{"status":"completed"}}}""",
                CreatedAt = now,
                UpdatedAt = now,
            });
            await db.SaveChangesAsync();
            db.ChangeTracker.Clear();

            var stored = await db.LearnerProgress.SingleAsync(p => p.UserId == "user-1");
            stored.SchemaVersion.ShouldBe(1);
            stored.UpdatedAt.ShouldBe(now);
            stored.Data.ShouldContain("\"totalXp\": 42"); // jsonb chuẩn hóa JSON (thêm khoảng trắng)

            var xp = await db.Database.SqlQuery<int>($"SELECT (data->>'totalXp')::int AS \"Value\" FROM learner_progress WHERE user_id = 'user-1'").SingleAsync();
            xp.ShouldBe(42);
        }
        finally
        {
            await db.Database.EnsureDeletedAsync();
        }
    }
}
