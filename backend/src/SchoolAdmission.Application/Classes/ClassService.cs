using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Application.Persistence;
using SchoolAdmission.Domain;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Application.Classes;

public sealed class ClassService(IAppDbContext db)
{
    public async Task<ClassSummary> CreateAsync(
        string? name,
        int totalSeats,
        CancellationToken cancellationToken)
    {
        var schoolClass = new SchoolClass
        {
            Id = $"c_{Guid.NewGuid():N}",
            Name = name!,
            TotalSeats = totalSeats,
            CreatedAt = DateTimeOffset.UtcNow
        };

        db.Classes.Add(schoolClass);
        await db.SaveChangesAsync(cancellationToken);

        return new ClassSummary(
            schoolClass.Id,
            schoolClass.Name,
            schoolClass.TotalSeats,
            0,
            0,
            schoolClass.TotalSeats);
    }

    public async Task<IReadOnlyList<ClassSummary>> ListAsync(
        CancellationToken cancellationToken)
    {
        var now = await db.GetDatabaseUtcNowAsync(cancellationToken);

        var rows = await db.Classes
            .AsNoTracking()
            .OrderBy(schoolClass => schoolClass.CreatedAt)
            .Select(schoolClass => new
            {
                schoolClass.Id,
                schoolClass.Name,
                schoolClass.TotalSeats,
                HeldSeats = schoolClass.Holds.Count(hold =>
                    hold.Status == HoldStatus.Held && hold.ExpiresAt > now),
                ConfirmedSeats = schoolClass.Holds.Count(hold =>
                    hold.Status == HoldStatus.Confirmed)
            })
            .ToListAsync(cancellationToken);

        return rows
            .Select(row => new ClassSummary(
                row.Id,
                row.Name,
                row.TotalSeats,
                row.HeldSeats,
                row.ConfirmedSeats,
                row.TotalSeats - row.HeldSeats - row.ConfirmedSeats))
            .ToList();
    }

    public async Task<ClassSummary?> GetAsync(
        string classId,
        CancellationToken cancellationToken)
    {
        var now = await db.GetDatabaseUtcNowAsync(cancellationToken);

        var row = await db.Classes
            .AsNoTracking()
            .Where(schoolClass => schoolClass.Id == classId)
            .Select(schoolClass => new
            {
                schoolClass.Id,
                schoolClass.Name,
                schoolClass.TotalSeats,
                HeldSeats = schoolClass.Holds.Count(hold =>
                    hold.Status == HoldStatus.Held && hold.ExpiresAt > now),
                ConfirmedSeats = schoolClass.Holds.Count(hold =>
                    hold.Status == HoldStatus.Confirmed)
            })
            .SingleOrDefaultAsync(cancellationToken);

        return row is null
            ? null
            : new ClassSummary(
                row.Id,
                row.Name,
                row.TotalSeats,
                row.HeldSeats,
                row.ConfirmedSeats,
                row.TotalSeats - row.HeldSeats - row.ConfirmedSeats);
    }
}
