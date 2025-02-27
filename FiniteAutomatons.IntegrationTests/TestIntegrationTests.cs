using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace FiniteAutomatons.IntegrationTests
{

    [Collection("Integration Tests")]
    public class TestIntegrationTests : IntegrationTestsBase
    {
        public TestIntegrationTests(IntegrationTestsFixture fixture) : base(fixture) { }

        [Fact]
        public async Task Test_ApplicationRunning_ReturnsSuccess()
        {
            // Arrange
            var client = GetHttpClient();

            // Act
            var response = await client.GetAsync("/");

            // Assert
            response.EnsureSuccessStatusCode();
        }

        [Fact]
        public async Task Test_AddUserAndVerify_IdentityWorks()
        {
            // Arrange
            var userName = "testuser@example.com";
            var password = "Test@1234";

            using (var scope = GetServiceScope())
            {
                var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

                // Act
                var user = new IdentityUser { UserName = userName, Email = userName };
                var result = await userManager.CreateAsync(user, password);

                // Assert
                result.Succeeded.ShouldBeTrue("User creation should succeed");

                var createdUser = await userManager.FindByNameAsync(userName);
                createdUser.ShouldNotBeNull();
                createdUser.UserName.ShouldBe(userName);
            }
        }
    }
}
