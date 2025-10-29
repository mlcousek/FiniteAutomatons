using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;

namespace FiniteAutomatons.IntegrationTests;


[Collection("Integration Tests")]
public class TestIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
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
        var userName = $"testuser{Guid.NewGuid()}@example.com";
        var password = "Test@1234";

        using var scope = GetServiceScope();
        
        // Ensure database is created
        var dbContext = scope.ServiceProvider.GetRequiredService<FiniteAutomatons.Data.ApplicationDbContext>();
        await dbContext.Database.EnsureCreatedAsync();
        
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<IdentityUser>>();

        // Act
        var user = new IdentityUser { UserName = userName, Email = userName };
        var result = await userManager.CreateAsync(user, password);

        // Assert
        result.Succeeded.ShouldBeTrue($"User creation should succeed. Errors: {string.Join(", ", result.Errors.Select(e => e.Description))}");

        var createdUser = await userManager.FindByNameAsync(userName);
        createdUser.ShouldNotBeNull();
        createdUser.UserName.ShouldBe(userName);
    }
}
