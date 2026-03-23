//using Aspire.Hosting.Testing;
//using FiniteAutomatons.Data;
//using Microsoft.EntityFrameworkCore;
//using Shouldly;

//namespace FiniteAutomatons.IntegrationTests.AspireTests;

//public class AspireAppHostTests
//{
//    [Fact]
//    public async Task AppHostStartsSuccessfully()
//    {
//        var appHost = await DistributedApplicationTestingBuilder
//            .CreateAsync<Projects.FiniteAutomatons_AppHost>();

//        await using var app = await appHost.BuildAsync();
//        await app.StartAsync();

//        await Task.Delay(2000);
//    }

//    [Fact]
//    public async Task WebAppIsHealthy()
//    {
//        var appHost = await DistributedApplicationTestingBuilder
//            .CreateAsync<Projects.FiniteAutomatons_AppHost>();

//        await using var app = await appHost.BuildAsync();
//        await app.StartAsync();

//        await Task.Delay(2000);

//        var httpClient = app.CreateHttpClient("finiteautomatons");

//        var response = await httpClient.GetAsync("/health");

//        response.IsSuccessStatusCode.ShouldBeTrue();
//    }

//    [Fact]
//    public async Task SqlServerIsReachable()
//    {
//        var appHost = await DistributedApplicationTestingBuilder
//            .CreateAsync<Projects.FiniteAutomatons_AppHost>();

//        await using var app = await appHost.BuildAsync();
//        await app.StartAsync();

//        await Task.Delay(2000);

//        var connectionString = await app.GetConnectionStringAsync("finiteautomatonsdb");

//        connectionString.ShouldNotBeNullOrWhiteSpace();

//        var optionsBuilder = new DbContextOptionsBuilder<ApplicationDbContext>();
//        optionsBuilder.UseSqlServer(connectionString);

//        await using var context = new ApplicationDbContext(optionsBuilder.Options);
//        var canConnect = await context.Database.CanConnectAsync();

//        canConnect.ShouldBeTrue();
//    }
//} TODO fix aspire tests in pipeline
