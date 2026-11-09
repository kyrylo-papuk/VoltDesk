using Microsoft.AspNetCore.Identity;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using VoltDesk.Core.Employees;

namespace VoltDesk.Infrastructure.Persistence;

public static class DatabaseInitializer
{
    /// <summary>
    /// Applies pending migrations and optionally seeds demo data. Waits for SQL Server to accept
    /// connections first, which matters when the API and the database start together.
    /// </summary>
    public static async Task InitializeAsync(
        IServiceProvider services,
        bool seedDemoData,
        CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        var provider = scope.ServiceProvider;
        var db = provider.GetRequiredService<VoltDeskDbContext>();
        var logger = provider.GetRequiredService<ILoggerFactory>().CreateLogger(typeof(DatabaseInitializer));

        const int maxAttempts = 10;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await db.Database.MigrateAsync(cancellationToken);
                break;
            }
            catch (SqlException ex) when (attempt < maxAttempts)
            {
                logger.LogWarning("Database is not ready (attempt {Attempt}/{Max}): {Message}", attempt, maxAttempts, ex.Message);
                await Task.Delay(TimeSpan.FromSeconds(3), cancellationToken);
            }
        }

        if (seedDemoData)
        {
            var hasher = provider.GetRequiredService<IPasswordHasher<Employee>>();
            var clock = provider.GetRequiredService<TimeProvider>();
            var today = DateOnly.FromDateTime(clock.GetLocalNow().DateTime);
            await DemoDataSeeder.SeedAsync(db, hasher, today, cancellationToken);
        }
    }
}
