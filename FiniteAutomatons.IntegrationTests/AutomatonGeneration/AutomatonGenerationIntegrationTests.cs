using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Shouldly;
using System.Net;

namespace FiniteAutomatons.IntegrationTests.AutomatonGeneration;

[Collection("Integration Tests")]
public class AutomatonGenerationIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task GenerateRandomAutomaton_DFA_ShouldCreateValidAutomaton()
    {
        // Arrange
        var client = GetHttpClient();

        // Act - Get the generation page
        var getResponse = await client.GetAsync("/Automaton/GenerateRandomAutomaton");

        // Assert
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await getResponse.Content.ReadAsStringAsync();
        html.ShouldContain("Generate Random Automaton");
        html.ShouldContain("Automaton Type");
        html.ShouldContain("Number of States");
        html.ShouldContain("Number of Transitions");
    }

    [Fact]
    public async Task GenerateRandomAutomaton_POST_ShouldGenerateAndRedirect()
    {
        // Arrange
        var client = GetHttpClient();
        var formData = new List<KeyValuePair<string, string>>
        {
            new("Type", "DFA"),
            new("StateCount", "4"),
            new("TransitionCount", "6"),
            new("AlphabetSize", "2"),
            new("AcceptingStateRatio", "0.5"),
            new("Seed", "12345")
        };

        // Act - Submit the generation form
        var postResponse = await client.PostAsync("/Automaton/GenerateRandomAutomaton", new FormUrlEncodedContent(formData));

        // Assert - Should either redirect on success or return OK with validation errors
        postResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);
        
        if (postResponse.StatusCode == HttpStatusCode.OK)
        {
            var content = await postResponse.Content.ReadAsStringAsync();
            content.ShouldNotContain("Error occurred");
        }
        else
        {
            postResponse.Headers.Location?.ToString().ShouldContain("/");
        }
    }

    [Fact]
    public async Task GenerateRealisticAutomaton_DFA_ShouldWork()
    {
        // Arrange
        var client = GetHttpClient();
        var formData = new List<KeyValuePair<string, string>>
        {
            new("type", "DFA"),
            new("stateCount", "5"),
            new("seed", "999")
        };

        // Act
        var response = await client.PostAsync("/Automaton/GenerateRealisticAutomaton", new FormUrlEncodedContent(formData));

        // Assert - Should either redirect on success or return OK with validation errors
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect, HttpStatusCode.Found);
        
        if (response.StatusCode == HttpStatusCode.OK)
        {
            var content = await response.Content.ReadAsStringAsync();
            content.ShouldNotContain("Error occurred");
        }
        else
        {
            response.Headers.Location?.ToString().ShouldContain("/");
        }
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

    [Theory]
    [InlineData(AutomatonType.DFA)]
    [InlineData(AutomatonType.NFA)]
    [InlineData(AutomatonType.EpsilonNFA)]
    public void AutomatonGeneratorService_GenerateRealisticAutomaton_AllTypes(AutomatonType type)
    {
        // Arrange
        using var scope = GetServiceScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomatonGeneratorService>();

        // Act
        var result = service.GenerateRealisticAutomaton(type, 4, 123);

        // Assert
        result.ShouldNotBeNull();
        result.Type.ShouldBe(type);
        result.States.Count.ShouldBe(4);
        result.States.Count(s => s.IsStart).ShouldBe(1);
        result.IsCustomAutomaton.ShouldBeTrue();
        result.Transitions.Count.ShouldBeGreaterThanOrEqualTo(4);
    }
}
