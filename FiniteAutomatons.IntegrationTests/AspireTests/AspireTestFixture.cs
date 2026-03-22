using Aspire.Hosting.Testing;
using FiniteAutomatons.Data;
using Microsoft.EntityFrameworkCore;

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

        HttpClient = App.CreateHttpClient("finiteautomatons");
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
