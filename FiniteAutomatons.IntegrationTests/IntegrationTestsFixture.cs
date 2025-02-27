using Testcontainers.MsSql;

namespace FiniteAutomatons.IntegrationTests
{
    public class IntegrationTestsFixture : IAsyncLifetime
    {
        private readonly MsSqlContainer msSqlContainer = new MsSqlBuilder()
            .WithImage("mcr.microsoft.com/mssql/server:2022-latest")
            .Build();

        private string? connectionString;
        private AutomatonsWebApplicationFactory<Program>? applicationFactory;

        public async Task InitializeAsync()
        {
            var startDb = StartDb();
            await Task.WhenAll(startDb);

            connectionString = await startDb;

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

        public AutomatonsWebApplicationFactory<Program> AutomatonsWebApplicationFactory => applicationFactory!;

        private AutomatonsWebApplicationFactory<Program> CreateWebApplicationFactory()
        {
            return new AutomatonsWebApplicationFactory<Program>(connectionString!);
        }

        private void SetEnviromentVariables()
        {
            Environment.SetEnvironmentVariable("ConnectionString__DbConnection", connectionString);
        }

    }

}
