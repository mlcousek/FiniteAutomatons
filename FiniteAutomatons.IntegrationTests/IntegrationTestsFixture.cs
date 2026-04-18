using Testcontainers.MsSql;

namespace FiniteAutomatons.IntegrationTests;

public class IntegrationTestsFixture : IAsyncLifetime
{
    private readonly MsSqlContainer msSqlContainer = new MsSqlBuilder("mcr.microsoft.com/mssql/server:2022-latest")
        .Build();

    private string? connectionString;
    private AutomatonsWebApplicationFactory<Program>? applicationFactory;
    private string? tempEmailDirectory;

    public async Task InitializeAsync()
    {
        var startDb = StartDb();
        await Task.WhenAll(startDb);

        connectionString = await startDb;
        // Prepare test environment
        PrepareEmailPickupDirectory();
        SetEnviromentVariables();
        applicationFactory = CreateWebApplicationFactory();

        async Task<string> StartDb()
        {
            await msSqlContainer.StartAsync();

            return msSqlContainer.GetConnectionString();
        }
    }
    public async Task DisposeAsync()
    {
        var disposeDb = DisposeDb();

        await Task.WhenAll(disposeDb);

        async Task DisposeDb()
        {
            await msSqlContainer.DisposeAsync();
        }
    }

    private void PrepareEmailPickupDirectory()
    {
        // create a unique temp directory for email pickup files for this test run
        var baseTmp = Path.Combine(Path.GetTempPath(), "finiteautomatons_integration_emails");
        Directory.CreateDirectory(baseTmp);
        tempEmailDirectory = Path.Combine(baseTmp, Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempEmailDirectory);
    }

    public AutomatonsWebApplicationFactory<Program> AutomatonsWebApplicationFactory => applicationFactory!;

    private AutomatonsWebApplicationFactory<Program> CreateWebApplicationFactory()
    {
        return new AutomatonsWebApplicationFactory<Program>(connectionString!);
    }

    private void SetEnviromentVariables()
    {
        Environment.SetEnvironmentVariable("ConnectionString__DbConnection", connectionString);
        if (!string.IsNullOrEmpty(tempEmailDirectory))
        {
            Environment.SetEnvironmentVariable("Smtp__UsePickupDirectory", "true");
            Environment.SetEnvironmentVariable("Smtp__PickupDirectory", tempEmailDirectory);
            // ensure host is empty so sender will operate in pickup mode
            Environment.SetEnvironmentVariable("Smtp__Host", string.Empty);
        }
    }

    public string TempEmailDirectory => tempEmailDirectory!;
}
