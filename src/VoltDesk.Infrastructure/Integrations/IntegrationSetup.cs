using Microsoft.Extensions.DependencyInjection;
using VoltDesk.Infrastructure.Integrations.Google;

namespace VoltDesk.Infrastructure.Integrations;

public static class IntegrationSetup
{
    /// <summary>
    /// Registers the Google Workspace integration when it is enabled; otherwise events are dropped
    /// and Google features answer "not configured".
    /// </summary>
    public static IServiceCollection AddGoogleIntegration(this IServiceCollection services, GoogleOptions options)
    {
        services.AddScoped<GoogleFolders>();
        if (!options.Enabled)
        {
            services.AddScoped<IIntegrationEvents, NoIntegrationEvents>();
            services.AddSingleton<IGoogleWorkspace, DisabledGoogleWorkspace>();
            return services;
        }

        services.AddScoped<IIntegrationEvents, OutboxIntegrationEvents>();
        services.AddSingleton<IGoogleWorkspace, GoogleWorkspace>();
        services.AddScoped<GoogleSyncProcessor>();
        if (options.RunWorker)
        {
            services.AddHostedService<GoogleSyncWorker>();
        }

        return services;
    }
}
