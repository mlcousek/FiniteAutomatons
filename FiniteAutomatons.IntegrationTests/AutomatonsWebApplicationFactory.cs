using FiniteAutomatons.Data;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace FiniteAutomatons.IntegrationTests;

public class AutomatonsWebApplicationFactory<TProgram>(string dbConnetionString) : WebApplicationFactory<TProgram> where TProgram : class
{
    private readonly string dbConnetionString = dbConnetionString ?? throw new ArgumentNullException(nameof(dbConnetionString));
    protected override IHost CreateHost(IHostBuilder builder)
    {
        try
        {
            var host = base.CreateHost(builder);

            Task.Delay(200).Wait();

            return host;
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.Message);
            throw;
        }
    }
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Development");
        builder.ConfigureServices(services =>
        {
            // Remove the existing DbContext registration
            var descriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<ApplicationDbContext>));
            if (descriptor != null)
            {
                services.Remove(descriptor);
            }

            // Add a new DbContext registration for the test database
            services.AddDbContext<ApplicationDbContext>(options =>
            {
                // options.UseSqlServer("Server=localhost,1433;Database=TestDb;User Id=sa;Password=YourStrong!Passw0rd;");
                options.UseSqlServer(dbConnetionString);
            });

            // Build the service provider
            var serviceProvider = services.BuildServiceProvider();

            // Create the test database
            using (var scope = serviceProvider.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                db.Database.EnsureCreated();
            }
        });
    }
}
