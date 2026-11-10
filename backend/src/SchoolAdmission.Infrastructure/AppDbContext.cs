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

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(AppDbContext).Assembly);
        base.OnModelCreating(modelBuilder);
    }
}
