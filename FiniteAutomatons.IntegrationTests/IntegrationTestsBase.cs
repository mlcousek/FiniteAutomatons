using Microsoft.Extensions.DependencyInjection;

namespace FiniteAutomatons.IntegrationTests
{
    public abstract class IntegrationTestsBase(IntegrationTestsFixture fixture)
    {
        protected readonly IntegrationTestsFixture fixture = fixture ?? throw new ArgumentNullException(nameof(fixture));

        protected HttpClient GetHttpClient()
        {
            var appFactory = fixture.AutomatonsWebApplicationFactory;
            var client = appFactory.CreateClient();
            return client;
        }
        protected IServiceScope GetServiceScope()
        {
            var appFactory = fixture.AutomatonsWebApplicationFactory;
            var scope = appFactory.Services.CreateScope();
            return scope;
        }
    }
}
