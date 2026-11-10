using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using SchoolAdmission.Application.Persistence;
using SchoolAdmission.Domain.Entities;

namespace SchoolAdmission.Infrastructure;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options)
    : DbContext(options), IAppDbContext
{
    public DbSet<SchoolClass> Classes => Set<SchoolClass>();
    public DbSet<Hold> Holds => Set<Hold>();

    public async Task<DateTimeOffset> GetDatabaseUtcNowAsync(
        CancellationToken cancellationToken)
    {
        var now = await Database
            .SqlQueryRaw<DateTime>("SELECT (now() AT TIME ZONE 'UTC') AS \"Value\"")
            .SingleAsync(cancellationToken);

        return new DateTimeOffset(DateTime.SpecifyKind(now, DateTimeKind.Utc));
    }

    public Task<IDbContextTransaction> BeginTransactionAsync(
        CancellationToken cancellationToken = default) =>
        Database.BeginTransactionAsync(cancellationToken);

    public async Task<SchoolClass?> LockClassForUpdateAsync(
        string classId,
        CancellationToken cancellationToken)
    {
        var classes = await Classes
            .FromSqlRaw("SELECT * FROM classes WHERE id = {0} FOR UPDATE", classId)
            .ToListAsync(cancellationToken);

        return classes.SingleOrDefault();
    }

    public async Task<Hold?> LockHoldForUpdateAsync(
        string holdId,
        CancellationToken cancellationToken)
    {
        var holds = await Holds
            .FromSqlRaw("SELECT * FROM holds WHERE id = {0} FOR UPDATE", holdId)
            .ToListAsync(cancellationToken);

        return holds.SingleOrDefault();
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
