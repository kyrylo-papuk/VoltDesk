using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VoltDesk.Infrastructure.Persistence;

namespace VoltDesk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        services.AddDbContext<VoltDeskDbContext>(options =>
            options.UseSqlServer(connectionString, sql => sql.EnableRetryOnFailure()));

        return services;
    }
}
