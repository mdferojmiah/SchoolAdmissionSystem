using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using SchoolAdmission.Infrastructure;

namespace SchoolAdmission.Api.Startup;

public static class DatabaseMigrator
{
    public static async Task ApplyMigrationsAsync(
        IServiceProvider services,
        int maxAttempts = 10,
        TimeSpan? retryDelay = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(maxAttempts, 1);
        retryDelay ??= TimeSpan.FromSeconds(2);

        await using var scope = services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                return;
            }
            catch (Exception ex) when (attempt < maxAttempts && ex is not OperationCanceledException)
            {
                logger.LogWarning(
                    ex,
                    "Database migration attempt {Attempt}/{MaxAttempts} failed. Retrying in {Delay}.",
                    attempt,
                    maxAttempts,
                    retryDelay);
                await Task.Delay(retryDelay.Value, cancellationToken);
            }
        }
    }
}
