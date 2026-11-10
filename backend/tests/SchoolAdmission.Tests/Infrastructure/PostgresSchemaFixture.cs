using Microsoft.EntityFrameworkCore;
using SchoolAdmission.Infrastructure;

namespace SchoolAdmission.Tests.Infrastructure;

public sealed class PostgresSchemaFixture : IAsyncLifetime
{
    public string ConnectionString { get; private set; } = null!;
    public DbContextOptions<AppDbContext> Options { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        ConnectionString =
            Environment.GetEnvironmentVariable("ConnectionStrings__Default")
            ?? Environment.GetEnvironmentVariable("DATABASE_URL")
            ?? throw new InvalidOperationException(
                "Run database tests with: docker compose run --rm backend-tests");

        Options = new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(ConnectionString)
            .Options;

        await using var db = new AppDbContext(Options);
        await db.Database.MigrateAsync();
    }

    public Task DisposeAsync() => Task.CompletedTask;
}

[CollectionDefinition("PostgresSchema", DisableParallelization = true)]
public sealed class PostgresSchemaCollection : ICollectionFixture<PostgresSchemaFixture>;
