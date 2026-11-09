using Microsoft.EntityFrameworkCore;
using VoltDesk.Infrastructure;
using VoltDesk.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("VoltDesk")
    ?? throw new InvalidOperationException("Connection string 'VoltDesk' is not configured.");

builder.Services.AddInfrastructure(connectionString);
builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddHealthChecks()
    .AddDbContextCheck<VoltDeskDbContext>("database");

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();

    // Local convenience: create or upgrade the dev database and fill it with demo data on start.
    await using var scope = app.Services.CreateAsyncScope();
    var db = scope.ServiceProvider.GetRequiredService<VoltDeskDbContext>();
    await db.Database.MigrateAsync();
    await DemoDataSeeder.SeedAsync(db, DateOnly.FromDateTime(DateTime.Today));
}

app.MapControllers();
app.MapHealthChecks("/health");

app.Run();

// Exposed for WebApplicationFactory in integration tests.
public partial class Program;
