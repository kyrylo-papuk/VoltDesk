using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Testcontainers.MsSql;
using VoltDesk.Contracts;
using VoltDesk.Infrastructure.Integrations.Google;
using VoltDesk.Infrastructure.Persistence;

[assembly: AssemblyFixture(typeof(VoltDesk.Api.Tests.ApiFactory))]

namespace VoltDesk.Api.Tests;

/// <summary>
/// Runs the real API against SQL Server in a throwaway container. Migrations and demo data are
/// applied once per test run; tests create their own records so they can run in parallel.
/// </summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    public const string Admin = "avery.stone@voltdesk.example";
    public const string Manager = "dana.whitfield@voltdesk.example";
    public const string Engineer = "sam.ortega@voltdesk.example";

    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    private readonly SemaphoreSlim _outboxGate = new(1, 1);

    /// <summary>Stands in for Google; the background worker is off and tests deliver the outbox themselves.</summary>
    public FakeGoogleWorkspace Google { get; } = new();

    private readonly MsSqlContainer _database = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2025-latest").Build();

    public async ValueTask InitializeAsync()
    {
        await _database.StartAsync();

        // Starting the server runs Program, which migrates and seeds the database.
        _ = Server;

        // Create the shared Drive root up front, so parallel tests never race to create it.
        await using var scope = Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<GoogleFolders>().EnsureRootAsync(CancellationToken.None);
    }

    public override async ValueTask DisposeAsync()
    {
        await base.DisposeAsync();
        await _database.DisposeAsync();
    }

    public async Task<HttpClient> SignInAsync(string email, string password = DemoDataSeeder.DemoPassword)
    {
        var client = CreateClient();
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest(email, password), Json, TestContext.Current.CancellationToken);
        response.EnsureSuccessStatusCode();
        var login = await response.Content.ReadFromJsonAsync<LoginResponse>(Json, TestContext.Current.CancellationToken);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", login!.AccessToken);
        return client;
    }

    /// <summary>Delivers pending outbox messages, one test at a time so deliveries never overlap.</summary>
    public async Task ProcessOutboxAsync()
    {
        await _outboxGate.WaitAsync(TestContext.Current.CancellationToken);
        try
        {
            await using var scope = Services.CreateAsyncScope();
            await scope.ServiceProvider.GetRequiredService<GoogleSyncProcessor>().ProcessDueAsync(TestContext.Current.CancellationToken);
        }
        finally
        {
            _outboxGate.Release();
        }
    }

    public async Task<T> WithDbAsync<T>(Func<VoltDeskDbContext, Task<T>> query)
    {
        await using var scope = Services.CreateAsyncScope();
        return await query(scope.ServiceProvider.GetRequiredService<VoltDeskDbContext>());
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:VoltDesk", _database.GetConnectionString());
        builder.UseSetting("Jwt:SigningKey", "integration-tests-signing-key-0123456789");
        builder.UseSetting("Database:MigrateOnStartup", "true");
        builder.UseSetting("Database:SeedDemoData", "true");
        builder.UseSetting("Auth:LoginAttemptsPerMinute", "1000");
        builder.UseSetting("Google:Enabled", "true");
        builder.UseSetting("Google:RunWorker", "false");
        builder.ConfigureTestServices(services => services.AddSingleton<IGoogleWorkspace>(Google));
    }
}
