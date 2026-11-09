using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VoltDesk.Core.Employees;
using VoltDesk.Infrastructure.Persistence;

namespace VoltDesk.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, string connectionString)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(connectionString);

        // No retrying execution strategy: every save opens a transaction to write data and audit
        // entries together, and a retrying strategy cannot safely replay such a save.
        services.AddDbContext<VoltDeskDbContext>(options => options.UseSqlServer(connectionString));

        services.TryAddSingleton(TimeProvider.System);
        services.TryAddScoped<ICurrentActor, SystemActor>();
        services.TryAddSingleton<IPasswordHasher<Employee>, PasswordHasher<Employee>>();

        return services;
    }
}
