using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Application.Persistence;

public interface IAppDbContext
{
    DbSet<SchoolClass> Classes { get; }
    DbSet<Hold> Holds { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
