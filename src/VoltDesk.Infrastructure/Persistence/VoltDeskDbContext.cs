using Microsoft.EntityFrameworkCore;

namespace VoltDesk.Infrastructure.Persistence;

public sealed class VoltDeskDbContext(DbContextOptions<VoltDeskDbContext> options) : DbContext(options)
{
    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(VoltDeskDbContext).Assembly);
    }
}
