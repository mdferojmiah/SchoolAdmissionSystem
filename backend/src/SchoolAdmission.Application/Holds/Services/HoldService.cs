using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Application.Exceptions;
using SchoolAdmission.Application.Persistence;
using SchoolAdmission.Domain;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Application.Holds;

public sealed class HoldService(IAppDbContext db, HoldSettings settings)
{
    public async Task<HoldSummary> ApplyAsync(
        string classId,
        string studentId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var schoolClass = await db.LockClassForUpdateAsync(classId, cancellationToken);

        if (schoolClass is null)
        {
            throw new AppException(404, "CLASS_NOT_FOUND", "Class not found.");
        }

        var now = await db.GetDatabaseUtcNowAsync(cancellationToken);

        await db.Holds
            .Where(hold =>
                hold.ClassId == classId &&
                hold.StudentId == studentId &&
                hold.Status == HoldStatus.Held &&
                hold.ExpiresAt <= now)
            .ExecuteUpdateAsync(
                setters => setters.SetProperty(hold => hold.Status, HoldStatus.Expired),
                cancellationToken);

        var occupiedSeats = await db.Holds.CountAsync(
            hold =>
                hold.ClassId == classId &&
                (hold.Status == HoldStatus.Confirmed ||
                 (hold.Status == HoldStatus.Held && hold.ExpiresAt > now)),
            cancellationToken);

        if (occupiedSeats >= schoolClass.TotalSeats)
        {
            throw new AppException(
                409,
                "NO_SEATS",
                "No seats are available in this class.");
        }

        var hasActiveApplication = await db.Holds.AnyAsync(
            hold =>
                hold.ClassId == classId &&
                hold.StudentId == studentId &&
                (hold.Status == HoldStatus.Confirmed ||
                 (hold.Status == HoldStatus.Held && hold.ExpiresAt > now)),
            cancellationToken);

        if (hasActiveApplication)
        {
            throw new AppException(
                409,
                "DUPLICATE_APPLICATION",
                "You have already applied to this class.");
        }

        var hold = new Hold
        {
            Id = $"h_{Guid.NewGuid():N}",
            ClassId = classId,
            StudentId = studentId,
            Status = HoldStatus.Held,
            ExpiresAt = now.AddSeconds(settings.DurationSeconds),
            CreatedAt = now
        };

        db.Holds.Add(hold);
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new HoldSummary(
            hold.Id,
            hold.ClassId,
            hold.StudentId,
            hold.Status.ToString().ToUpperInvariant(),
            hold.ExpiresAt);
    }

    public async Task<HoldConfirmation> ConfirmAsync(
        string holdId,
        string studentId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var hold = await db.LockHoldForUpdateAsync(holdId, cancellationToken);

        if (hold is null)
        {
            throw new AppException(404, "HOLD_NOT_FOUND", "Hold not found.");
        }

        if (hold.StudentId != studentId)
        {
            throw new AppException(
                403,
                "NOT_HOLD_OWNER",
                "This application belongs to a different student.");
        }

        if (hold.Status == HoldStatus.Confirmed)
        {
            await transaction.CommitAsync(cancellationToken);
            return new HoldConfirmation(hold.Id, "CONFIRMED");
        }

        if (hold.Status == HoldStatus.Cancelled)
        {
            throw new AppException(
                409,
                "HOLD_CANCELLED",
                "This application was cancelled.");
        }

        var now = await db.GetDatabaseUtcNowAsync(cancellationToken);

        if (hold.Status == HoldStatus.Expired || hold.ExpiresAt <= now)
        {
            throw new AppException(
                409,
                "HOLD_EXPIRED",
                "Your hold has expired. Please apply again.");
        }

        hold.Status = HoldStatus.Confirmed;
        hold.ConfirmedAt = now;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);

        return new HoldConfirmation(hold.Id, "CONFIRMED");
    }

    public async Task CancelAsync(
        string holdId,
        string studentId,
        CancellationToken cancellationToken)
    {
        await using var transaction = await db.BeginTransactionAsync(cancellationToken);

        var hold = await db.LockHoldForUpdateAsync(holdId, cancellationToken);

        if (hold is null)
        {
            throw new AppException(404, "HOLD_NOT_FOUND", "Hold not found.");
        }

        if (hold.StudentId != studentId)
        {
            throw new AppException(
                403,
                "NOT_HOLD_OWNER",
                "This application belongs to a different student.");
        }

        if (hold.Status == HoldStatus.Cancelled)
        {
            throw new AppException(
                409,
                "HOLD_CANCELLED",
                "This application was cancelled.");
        }

        if (hold.Status == HoldStatus.Confirmed)
        {
            throw new AppException(
                409,
                "HOLD_NOT_CANCELLABLE",
                "A confirmed admission cannot be cancelled.");
        }

        var now = await db.GetDatabaseUtcNowAsync(cancellationToken);

        if (hold.Status == HoldStatus.Expired || hold.ExpiresAt <= now)
        {
            throw new AppException(
                409,
                "HOLD_EXPIRED",
                "Your hold has expired. Please apply again.");
        }

        hold.Status = HoldStatus.Cancelled;

        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<HoldDetails> GetAsync(
        string holdId,
        CancellationToken cancellationToken)
    {
        var hold = await db.Holds
            .AsNoTracking()
            .SingleOrDefaultAsync(candidate => candidate.Id == holdId, cancellationToken);

        if (hold is null)
        {
            throw new AppException(404, "HOLD_NOT_FOUND", "Hold not found.");
        }

        var now = await db.GetDatabaseUtcNowAsync(cancellationToken);

        return new HoldDetails(
            hold.Id,
            hold.ClassId,
            ToApiStatus(hold.Status, hold.ExpiresAt, now),
            hold.ExpiresAt);
    }

    public async Task<IReadOnlyList<MyHoldSummary>> ListMineAsync(
        string studentId,
        CancellationToken cancellationToken)
    {
        var now = await db.GetDatabaseUtcNowAsync(cancellationToken);

        var rows = await (
                from hold in db.Holds.AsNoTracking()
                join schoolClass in db.Classes.AsNoTracking()
                    on hold.ClassId equals schoolClass.Id
                where hold.StudentId == studentId
                orderby hold.CreatedAt descending
                select new
                {
                    hold.Id,
                    hold.ClassId,
                    ClassName = schoolClass.Name,
                    hold.Status,
                    hold.ExpiresAt
                })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new MyHoldSummary(
                row.Id,
                row.ClassId,
                row.ClassName,
                ToApiStatus(row.Status, row.ExpiresAt, now),
                row.ExpiresAt))
            .ToList();
    }

    private static string ToApiStatus(
        HoldStatus status,
        DateTimeOffset expiresAt,
        DateTimeOffset now) =>
        status == HoldStatus.Held && expiresAt <= now
            ? "EXPIRED"
            : status.ToString().ToUpperInvariant();
}
