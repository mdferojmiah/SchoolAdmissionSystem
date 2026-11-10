using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using SchoolAdmission.Api.Startup;
using SchoolAdmission.Infrastructure;

namespace SchoolAdmission.Tests.Infrastructure;

[Collection("PostgresSchema")]
public sealed class DatabaseMigratorTests(PostgresSchemaFixture fixture)
{
    [Fact]
    public async Task ApplyMigrationsAsync_LeavesNoPendingMigrations()
    {
        var services = new ServiceCollection()
            .AddLogging()
            .AddDbContext<AppDbContext>(options => options.UseNpgsql(fixture.ConnectionString))
            .BuildServiceProvider();

        await DatabaseMigrator.ApplyMigrationsAsync(services, maxAttempts: 1);

        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var pending = await db.Database.GetPendingMigrationsAsync();

        Assert.Empty(pending);
    }
}
