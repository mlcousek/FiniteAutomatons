using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Net;

namespace FiniteAutomatons.IntegrationTests.AutomatonOperations.AutomatonGeneration;

[Collection("Integration Tests")]
public class AutomatonGenerationIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task GenerateRandomAutomaton_POST_ShouldGenerateAndRedirect()
    {
        // Arrange
        var client = GetHttpClient();

        var formData = new List<KeyValuePair<string, string>>
        {
         new("Type", "0"), // DFA enum value
            new("StateCount", "4"),
        new("TransitionCount", "6"),
      new("AlphabetSize", "2"),
     new("AcceptingStateRatio", "0.5"),
     new("Seed", "12345")
   };

        // Act - Submit the generation form (client follows redirects by default)
        var postResponse = await client.PostAsync("/AutomatonGeneration/GenerateRandomAutomaton", new FormUrlEncodedContent(formData));

        // Assert - Should follow redirect and end up at Home/Index with 200 OK
        postResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await postResponse.Content.ReadAsStringAsync();
        // Should have the automaton loaded
        html.ShouldContain("AUTOMATON");
        html.ShouldContain("States");
    }

    [Fact]
    public async Task GenerateRandomAutomaton_POST_PDA_ShouldGenerateAndRedirect()
    {
        // Arrange
        var client = GetHttpClient();

        var formData = new List<KeyValuePair<string, string>>
        {
new("Type", "3"), // PDA enum value
            new("StateCount", "4"),
   new("TransitionCount", "8"),
      new("AlphabetSize", "3"),
    new("AcceptingStateRatio", "0.4"),
  new("Seed", "4242")
        };

        // Act
        var postResponse = await client.PostAsync("/AutomatonGeneration/GenerateRandomAutomaton", new FormUrlEncodedContent(formData));

        // Assert - Should follow redirect and end up at Home/Index
        postResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await postResponse.Content.ReadAsStringAsync();
        html.ShouldContain("AUTOMATON");
        html.ShouldContain("States");
    }

    [Fact]
    public void AutomatonGeneratorService_GenerateRandomAutomaton_ShouldWork()
    {
        // Arrange
        using var scope = GetServiceScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomatonGeneratorService>();

        // Act
        var result = service.GenerateRandomAutomaton(AutomatonType.DFA, 3, 4, 2, 0.33, 42);

        // Assert
        result.ShouldNotBeNull();
        result.Type.ShouldBe(AutomatonType.DFA);
        result.States.Count.ShouldBe(3);
        result.States.Count(s => s.IsStart).ShouldBe(1);
        result.Alphabet.Count.ShouldBe(2);
        result.IsCustomAutomaton.ShouldBeTrue();
    }
}
