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
}
