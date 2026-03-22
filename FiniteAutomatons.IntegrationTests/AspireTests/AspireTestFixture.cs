using Aspire.Hosting.Testing;
using FiniteAutomatons.Data;
using Microsoft.EntityFrameworkCore;
using System.Net.Http;

namespace FiniteAutomatons.IntegrationTests.AspireTests;

public class AspireTestFixture : IAsyncLifetime
{
    public Aspire.Hosting.DistributedApplication? App { get; private set; }
    public HttpClient? HttpClient { get; private set; }
    public string? ConnectionString { get; private set; }

    public async Task InitializeAsync()
    {
        var appHost = await DistributedApplicationTestingBuilder
            .CreateAsync<Projects.FiniteAutomatons_AppHost>();

        App = await appHost.BuildAsync();
        await App.StartAsync();

        // Create an HttpClient that will accept the test host's development certificate used by Aspire in CI runners.
        // We use a handler that accepts any server certificate only for test purposes.
        // Acquire the configured base address from the app's client, then create a client with a handler
        // that accepts the test certificate. Some Aspire hosts don't expose handler customization.
        using (var tmp = App.CreateHttpClient("finiteautomatons"))
        {
            var baseAddress = tmp.BaseAddress;

            var handler = new HttpClientHandler()
            {
                ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            };

            HttpClient = new HttpClient(handler) { BaseAddress = baseAddress };
        }
        ConnectionString = await App.GetConnectionStringAsync("finiteautomatonsdb");

        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
        optionsBuilder.UseSqlServer(ConnectionString);
        await using var dbContext = new ApplicationDbContext(optionsBuilder.Options);
        await dbContext.Database.EnsureCreatedAsync();

        await Task.Delay(1000);
    }

    public async Task DisposeAsync()
    {
        HttpClient?.Dispose();

        if (App != null)
        {
            await App.DisposeAsync();
        }
    }
}
