using System.Text.Json;
using System.Text.Json.Serialization;

using Hanbit.Application.Abstractions;
using Hanbit.Domain.Progress;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Hanbit.Infrastructure.Persistence;

/// <summary>
/// <see cref="IProgressStore"/> trên PostgreSQL (T8.3): cột <c>data</c> (jsonb) chứa <see cref="LearnerProgress"/> cùng định dạng
/// với localStorage / file sao lưu (camelCase, enum là chuỗi).
/// </summary>
public sealed class EfProgressStore(HanbitDbContext db, IClock clock) : IProgressStore
{
    /// <summary>Định dạng JSON lưu DB — giống API (camelCase, enum chuỗi camelCase).</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private const int MaxInsertAttempts = 3;

    public async Task<LearnerProgress?> GetAsync(string userId, CancellationToken cancellationToken)
    {
        var record = await db.LearnerProgress.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, cancellationToken);
        return record is null ? null : Deserialize(record.Data);
    }

    public async Task<LearnerProgress> UpdateAsync(string userId, Func<LearnerProgress?, LearnerProgress> update, CancellationToken cancellationToken)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await db.Database.CreateExecutionStrategy().ExecuteAsync(
                    (userId, update),
                    async (_, state, ct) => await UpdateOnceAsync(state.userId, state.update, ct),
                    verifySucceeded: null,
                    cancellationToken);
            }
            catch (DbUpdateException ex) when (ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } && attempt < MaxInsertAttempts)
            {
                // Hai thiết bị cùng tạo dòng đầu tiên → dòng đã có, thử lại sẽ đọc + khóa được nó.
                db.ChangeTracker.Clear();
            }
        }
    }

    private async Task<LearnerProgress> UpdateOnceAsync(string userId, Func<LearnerProgress?, LearnerProgress> update, CancellationToken cancellationToken)
    {
        db.ChangeTracker.Clear();
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);

        // Khóa dòng tới hết giao dịch: đồng bộ song song của cùng tài khoản chạy lần lượt.
        var record = (await db.LearnerProgress
            .FromSql($"SELECT * FROM learner_progress WHERE user_id = {userId} FOR UPDATE")
            .ToListAsync(cancellationToken)).SingleOrDefault();

        var next = update(record is null ? null : Deserialize(record.Data));
        var data = JsonSerializer.Serialize(next, JsonOptions);
        var now = clock.UtcNow;
        if (record is null)
        {
            db.LearnerProgress.Add(new LearnerProgressRecord
            {
                UserId = userId,
                SchemaVersion = next.SchemaVersion,
                Data = data,
                CreatedAt = now,
                UpdatedAt = now,
            });
        }
        else
        {
            record.SchemaVersion = next.SchemaVersion;
            record.Data = data;
            record.UpdatedAt = now;
        }

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return next;
    }

    private static LearnerProgress? Deserialize(string data) => JsonSerializer.Deserialize<LearnerProgress>(data, JsonOptions);
}
