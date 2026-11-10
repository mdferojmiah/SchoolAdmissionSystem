using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Application.Persistence;
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
        var classes = await db.Classes
            .AsNoTracking()
            .OrderBy(schoolClass => schoolClass.CreatedAt)
            .ToListAsync(cancellationToken);

        return classes
            .Select(schoolClass => new ClassSummary(
                schoolClass.Id,
                schoolClass.Name,
                schoolClass.TotalSeats,
                0,
                0,
                schoolClass.TotalSeats))
            .ToList();
    }

    public async Task<ClassSummary?> GetAsync(
        string classId,
        CancellationToken cancellationToken)
    {
        var schoolClass = await db.Classes
            .AsNoTracking()
            .SingleOrDefaultAsync(
                candidate => candidate.Id == classId,
                cancellationToken);

        return schoolClass is null
            ? null
            : new ClassSummary(
                schoolClass.Id,
                schoolClass.Name,
                schoolClass.TotalSeats,
                0,
                0,
                schoolClass.TotalSeats);
    }
}
