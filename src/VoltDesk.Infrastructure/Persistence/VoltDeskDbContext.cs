using Microsoft.EntityFrameworkCore;
using VoltDesk.Core.Clients;
using VoltDesk.Core.Employees;
using VoltDesk.Core.Panels;
using VoltDesk.Core.Projects;
using VoltDesk.Infrastructure.Integrations;
using VoltDesk.Infrastructure.Persistence.Auditing;

namespace VoltDesk.Infrastructure.Persistence;

public sealed class VoltDeskDbContext(
    DbContextOptions<VoltDeskDbContext> options,
    ICurrentActor actor,
    TimeProvider clock) : DbContext(options)
{
    public DbSet<Client> Clients => Set<Client>();

    public DbSet<Contact> Contacts => Set<Contact>();

    public DbSet<Employee> Employees => Set<Employee>();

    public DbSet<Project> Projects => Set<Project>();

    public DbSet<ProjectTask> ProjectTasks => Set<ProjectTask>();

    public DbSet<ElectricalPanel> Panels => Set<ElectricalPanel>();

    public DbSet<Circuit> Circuits => Set<Circuit>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();

    public DbSet<OutboxMessage> Outbox => Set<OutboxMessage>();

    public DbSet<ExternalLink> ExternalLinks => Set<ExternalLink>();

    public override int SaveChanges(bool acceptAllChangesOnSuccess) =>
        throw new NotSupportedException("Use SaveChangesAsync: saves are audited inside a transaction.");

    /// <summary>
    /// Saves changes and their audit entries in one transaction. Audit entries are written after
    /// the data so they can reference keys the database generated for new rows.
    /// </summary>
    public override async Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken cancellationToken = default)
    {
        if (!acceptAllChangesOnSuccess)
        {
            throw new NotSupportedException("Audited saves always accept changes on success.");
        }

        ChangeTracker.DetectChanges();
        var now = clock.GetUtcNow();
        Tracking.Stamp(ChangeTracker, now);
        var pending = AuditCollector.Capture(ChangeTracker);
        if (pending.Count == 0)
        {
            return await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        }

        await using var transaction = Database.CurrentTransaction is null
            ? await Database.BeginTransactionAsync(cancellationToken)
            : null;

        var saved = await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);
        AuditEntries.AddRange(pending.Select(p => AuditCollector.ToEntry(p, actor.EmployeeId, now)));
        await base.SaveChangesAsync(acceptAllChangesOnSuccess, cancellationToken);

        if (transaction is not null)
        {
            await transaction.CommitAsync(cancellationToken);
        }

        return saved;
    }

    protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder)
    {
        // Store enums as readable names so the database makes sense without the code.
        configurationBuilder.Properties<Enum>().HaveConversion<string>().HaveMaxLength(32);
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VoltDeskDbContext).Assembly);
    }
}
