using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using VoltDesk.Core.Projects;
using VoltDesk.Infrastructure.Persistence;

namespace VoltDesk.Infrastructure.Integrations.Google;

/// <summary>
/// Delivers outbox messages to Google: project folders in Drive and milestone deadlines in
/// Calendar. Failures are retried with exponential back-off and give up after <see cref="MaxAttempts"/>.
/// </summary>
public sealed class GoogleSyncProcessor(
    VoltDeskDbContext db,
    IGoogleWorkspace google,
    GoogleFolders folders,
    TimeProvider clock,
    ILogger<GoogleSyncProcessor> logger)
{
    public const int MaxAttempts = 8;
    private const int BatchSize = 20;

    /// <summary>Processes messages that are due now. Returns how many were attempted.</summary>
    public async Task<int> ProcessDueAsync(CancellationToken cancellationToken)
    {
        var now = clock.GetUtcNow();
        var due = await db.Outbox
            .Where(m => m.ProcessedAt == null && m.FailedAt == null && m.NextAttemptAt <= now)
            .OrderBy(m => m.Id)
            .Take(BatchSize)
            .ToListAsync(cancellationToken);

        foreach (var message in due)
        {
            try
            {
                await HandleAsync(message, cancellationToken);
                message.ProcessedAt = clock.GetUtcNow();
                message.LastError = null;
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                message.Attempts++;
                message.LastError = ex.Message.Length > 1000 ? ex.Message[..1000] : ex.Message;
                if (message.Attempts >= MaxAttempts)
                {
                    message.FailedAt = now;
                    logger.LogError(ex, "Giving up on outbox message {Id} ({Type}) after {Attempts} attempts", message.Id, message.Type, message.Attempts);
                }
                else
                {
                    // 30 s, 1 min, 2 min, 4 min… so a Google outage does not hammer the API.
                    message.NextAttemptAt = now + TimeSpan.FromSeconds(30 * Math.Pow(2, message.Attempts - 1));
                    logger.LogWarning(ex, "Outbox message {Id} ({Type}) failed, attempt {Attempts}", message.Id, message.Type, message.Attempts);
                }
            }

            await db.SaveChangesAsync(cancellationToken);
        }

        return due.Count;
    }

    private Task HandleAsync(OutboxMessage message, CancellationToken cancellationToken) => message.Type switch
    {
        nameof(ProjectCreated) => OnProjectCreatedAsync(Read<ProjectCreated>(message), cancellationToken),
        nameof(MilestonesChanged) => OnMilestonesChangedAsync(Read<MilestonesChanged>(message), cancellationToken),
        _ => throw new InvalidOperationException($"Unknown integration event '{message.Type}'."),
    };

    private async Task OnProjectCreatedAsync(ProjectCreated created, CancellationToken cancellationToken)
    {
        var project = await db.Projects.SingleAsync(p => p.Number == created.ProjectNumber, cancellationToken);
        var folder = await folders.EnsureProjectFolderAsync(project, cancellationToken);
        await SyncMilestonesAsync(project, folder.Url, cancellationToken);
    }

    private async Task OnMilestonesChangedAsync(MilestonesChanged changed, CancellationToken cancellationToken)
    {
        var project = await db.Projects.SingleAsync(p => p.Id == changed.ProjectId, cancellationToken);
        var folder = await folders.EnsureProjectFolderAsync(project, cancellationToken);
        await SyncMilestonesAsync(project, folder.Url, cancellationToken);
    }

    /// <summary>One all-day event per scheduled milestone, updated in place when dates change.</summary>
    private async Task SyncMilestonesAsync(Project project, string? folderUrl, CancellationToken cancellationToken)
    {
        foreach (var milestone in project.Milestones.Where(m => m.DueDate is not null))
        {
            var key = milestone.Stage.ToString();
            var link = await db.ExternalLinks.SingleOrDefaultAsync(
                l => l.Kind == ExternalLink.MilestoneEvent && l.ProjectId == project.Id && l.Key == key,
                cancellationToken);

            var eventId = await google.UpsertMilestoneEventAsync(
                link?.ExternalId,
                new MilestoneEventData(project.Number, project.Name, milestone.Stage, milestone.DueDate!.Value, milestone.CompletedOn, folderUrl),
                cancellationToken);

            if (link is null)
            {
                db.ExternalLinks.Add(new ExternalLink { Kind = ExternalLink.MilestoneEvent, ProjectId = project.Id, Key = key, ExternalId = eventId });
            }
            else
            {
                link.ExternalId = eventId;
            }

            await db.SaveChangesAsync(cancellationToken);
        }
    }

    private static T Read<T>(OutboxMessage message) =>
        JsonSerializer.Deserialize<T>(message.Payload) ?? throw new InvalidOperationException($"Empty payload in message {message.Id}.");
}

/// <summary>Polls the outbox in the background.</summary>
internal sealed class GoogleSyncWorker(
    IServiceScopeFactory scopes,
    IOptions<GoogleOptions> options,
    ILogger<GoogleSyncWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(options.Value.PollInterval);
        do
        {
            try
            {
                await using var scope = scopes.CreateAsyncScope();
                await scope.ServiceProvider.GetRequiredService<GoogleSyncProcessor>().ProcessDueAsync(stoppingToken);
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogError(ex, "Google sync pass failed");
            }
        }
        while (await timer.WaitForNextTickAsync(stoppingToken));
    }
}
