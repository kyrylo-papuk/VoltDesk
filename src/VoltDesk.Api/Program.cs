using System.Text.Json.Serialization;
using VoltDesk.Api;
using VoltDesk.Api.Auth;
using VoltDesk.Api.Errors;
using VoltDesk.Infrastructure;
using VoltDesk.Infrastructure.Persistence;

var builder = WebApplication.CreateBuilder(args);

var connectionString = builder.Configuration.GetConnectionString("VoltDesk")
    ?? throw new InvalidOperationException("Connection string 'VoltDesk' is not configured.");

builder.Services.AddVoltDeskAuth(builder.Configuration);
builder.Services.AddInfrastructure(connectionString);
builder.Services.AddControllers()
    .AddJsonOptions(options => options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()));
builder.Services.AddProblemDetails();
builder.Services.AddExceptionHandler<ApiExceptionHandler>();
builder.Services.AddOpenApi(options => options.AddDocumentTransformer<BearerSecurityTransformer>());
builder.Services.AddHealthChecks()
    .AddDbContextCheck<VoltDeskDbContext>("database");

var app = builder.Build();

app.UseExceptionHandler();
app.UseStatusCodePages();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi().AllowAnonymous();
    app.UseSwaggerUI(options => options.SwaggerEndpoint("/openapi/v1.json", "VoltDesk API"));
}

app.UseAuthentication();
app.UseAuthorization();
app.UseRateLimiter();

app.MapControllers();
app.MapHealthChecks("/health").AllowAnonymous();

if (app.Configuration.GetValue<bool>("Database:MigrateOnStartup"))
{
    await DatabaseInitializer.InitializeAsync(app.Services, app.Configuration.GetValue<bool>("Database:SeedDemoData"));
}

app.Run();

// Exposed for WebApplicationFactory in integration tests.
public partial class Program;
