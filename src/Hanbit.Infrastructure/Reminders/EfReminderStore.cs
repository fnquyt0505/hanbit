using Hanbit.Application.Abstractions;
using Hanbit.Domain.Reminders;
using Hanbit.Infrastructure.Persistence;

using Microsoft.EntityFrameworkCore;

using Npgsql;

namespace Hanbit.Infrastructure.Reminders;

/// <summary><see cref="IReminderStore"/> trên PostgreSQL (bảng <c>reminder_settings</c>, <c>push_subscriptions</c>).</summary>
public sealed class EfReminderStore(HanbitDbContext db, IClock clock) : IReminderStore
{
    public async Task<ReminderSettings> GetSettingsAsync(string userId, CancellationToken cancellationToken)
    {
        var record = await db.ReminderSettings.AsNoTracking().SingleOrDefaultAsync(r => r.UserId == userId, cancellationToken);
        return record is null ? ReminderSettings.Default : new ReminderSettings(record.Enabled, record.ReminderTime, record.LastSentOn);
    }

    public async Task SaveSettingsAsync(string userId, bool enabled, TimeOnly time, CancellationToken cancellationToken)
    {
        var record = await db.ReminderSettings.SingleOrDefaultAsync(r => r.UserId == userId, cancellationToken);
        if (record is null)
        {
            db.ReminderSettings.Add(new ReminderSettingsRecord { UserId = userId, Enabled = enabled, ReminderTime = time, UpdatedAt = clock.UtcNow });
        }
        else
        {
            record.Enabled = enabled;
            record.ReminderTime = time;
            record.UpdatedAt = clock.UtcNow;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            // Hai request cùng tạo dòng — dòng kia đã thắng, ghi lại lên dòng đó.
            db.ChangeTracker.Clear();
            await SaveSettingsAsync(userId, enabled, time, cancellationToken);
        }
    }

    public async Task<IReadOnlyList<PushDevice>> GetDevicesAsync(string userId, CancellationToken cancellationToken) =>
        await db.PushSubscriptions.AsNoTracking()
            .Where(s => s.UserId == userId)
            .OrderBy(s => s.Id)
            .Select(s => new PushDevice(s.Endpoint, s.P256dh, s.Auth))
            .ToListAsync(cancellationToken);

    public async Task SaveDeviceAsync(string userId, PushDevice device, CancellationToken cancellationToken)
    {
        var record = await db.PushSubscriptions.SingleOrDefaultAsync(s => s.Endpoint == device.Endpoint, cancellationToken);
        if (record is null)
        {
            db.PushSubscriptions.Add(new PushSubscriptionRecord
            {
                UserId = userId,
                Endpoint = device.Endpoint,
                P256dh = device.P256dh,
                Auth = device.Auth,
                CreatedAt = clock.UtcNow,
            });
        }
        else
        {
            // Cùng trình duyệt, tài khoản khác đăng nhập → thiết bị thuộc tài khoản mới.
            record.UserId = userId;
            record.P256dh = device.P256dh;
            record.Auth = device.Auth;
        }

        try
        {
            await db.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException ex) when (IsUniqueViolation(ex))
        {
            db.ChangeTracker.Clear();
            await SaveDeviceAsync(userId, device, cancellationToken);
        }
    }

    public Task RemoveDeviceAsync(string userId, string endpoint, CancellationToken cancellationToken) =>
        db.PushSubscriptions.Where(s => s.UserId == userId && s.Endpoint == endpoint).ExecuteDeleteAsync(cancellationToken);

    public Task RemoveExpiredDeviceAsync(string endpoint, CancellationToken cancellationToken) =>
        db.PushSubscriptions.Where(s => s.Endpoint == endpoint).ExecuteDeleteAsync(cancellationToken);

    private static bool IsUniqueViolation(DbUpdateException ex) =>
        ex.InnerException is PostgresException { SqlState: PostgresErrorCodes.UniqueViolation };
}
