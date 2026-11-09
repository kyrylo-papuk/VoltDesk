using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using VoltDesk.Contracts;
using VoltDesk.Infrastructure.Persistence;

namespace VoltDesk.Api;

internal static class AuditQueries
{
    private const int MaxEntries = 200;

    /// <summary>Newest-first history of an aggregate, including changes of its child entities.</summary>
    public static async Task<List<AuditEntryDto>> HistoryAsync(
        this VoltDeskDbContext db,
        string rootType,
        int rootId,
        CancellationToken cancellationToken)
    {
        var rows = await (
                from entry in db.AuditEntries.AsNoTracking()
                where entry.RootType == rootType && entry.RootId == rootId
                join employee in db.Employees on entry.ActorId equals employee.Id into actors
                from actor in actors.DefaultIfEmpty()
                orderby entry.OccurredAt descending, entry.Id descending
                select new { Entry = entry, ActorName = actor == null ? null : actor.FullName })
            .Take(MaxEntries)
            .ToListAsync(cancellationToken);

        return
        [
            .. rows.Select(row => new AuditEntryDto(
                row.Entry.Id,
                row.Entry.OccurredAt,
                row.Entry.ActorId,
                row.ActorName,
                row.Entry.EntityType,
                row.Entry.EntityId,
                row.Entry.Action,
                JsonSerializer.Deserialize<Dictionary<string, AuditValueChange>>(row.Entry.Changes) ?? [])),
        ];
    }
}
