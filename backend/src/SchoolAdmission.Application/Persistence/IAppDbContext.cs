using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Application.Persistence;

public interface IAppDbContext
{
    DbSet<SchoolClass> Classes { get; }
    DbSet<Hold> Holds { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
    Task<DateTimeOffset> GetDatabaseUtcNowAsync(CancellationToken cancellationToken);
    Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default);
    Task<SchoolClass?> LockClassForUpdateAsync(
        string classId,
        CancellationToken cancellationToken);
}
