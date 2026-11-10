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

        var schoolClass = await db.Classes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == classId,
                cancellationToken);

        if (schoolClass is null)
        {
            throw new AppException(404, "CLASS_NOT_FOUND", "Class not found.");
        }

        var now = await db.GetDatabaseUtcNowAsync(cancellationToken);

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
}
