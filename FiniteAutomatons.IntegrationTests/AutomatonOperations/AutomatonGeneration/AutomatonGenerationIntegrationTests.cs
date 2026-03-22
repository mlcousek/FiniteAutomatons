using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
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
            new("Type", "0"),
            new("StateCount", "4"),
            new("TransitionCount", "6"),
            new("AlphabetSize", "2"),
            new("AcceptingStateRatio", "0.5"),
            new("Seed", "12345")
        };

        // Act
        var postResponse = await client.PostAsync("/AutomatonGeneration/GenerateRandomAutomaton", new FormUrlEncodedContent(formData));

        // Assert 
        postResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await postResponse.Content.ReadAsStringAsync();
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
            new("Type", "3"),
            new("StateCount", "4"),
            new("TransitionCount", "8"),
            new("AlphabetSize", "3"),
            new("AcceptingStateRatio", "0.4"),
            new("Seed", "4242")
        };

        // Act
        var postResponse = await client.PostAsync("/AutomatonGeneration/GenerateRandomAutomaton", new FormUrlEncodedContent(formData));

        // Assert 
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

    #region PDA Random Generation Tests

    [Fact]
    public async Task GenerateRandomPda_SmallStates_ShouldGenerateAndRedirect()
    {
        var client = GetHttpClient();
        var formData = new List<KeyValuePair<string, string>>
        {
            new("Type", "3"), // PDA
            new("StateCount", "3"),
            new("TransitionCount", "5"),
            new("AlphabetSize", "2"),
            new("AcceptingStateRatio", "0.5"),
            new("Seed", "111")
        };

        var response = await client.PostAsync("/AutomatonGeneration/GenerateRandomAutomaton", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("AUTOMATON");
        html.ShouldContain("States");
    }

    [Fact]
    public async Task GenerateRandomPda_LargerStates_ShouldGenerateAndRedirect()
    {
        var client = GetHttpClient();
        var formData = new List<KeyValuePair<string, string>>
        {
            new("Type", "3"), // PDA
            new("StateCount", "10"),
            new("TransitionCount", "20"),
            new("AlphabetSize", "3"),
            new("AcceptingStateRatio", "0.3"),
            new("Seed", "222")
        };

        var response = await client.PostAsync("/AutomatonGeneration/GenerateRandomAutomaton", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("AUTOMATON");
    }

    [Fact]
    public async Task GenerateRandomPda_VariousAlphabetSizes_ShouldWork()
    {
        var client = GetHttpClient();
        var formData = new List<KeyValuePair<string, string>>
        {
            new("Type", "3"), // PDA
            new("StateCount", "5"),
            new("TransitionCount", "8"),
            new("AlphabetSize", "5"),
            new("AcceptingStateRatio", "0.4"),
            new("Seed", "333")
        };

        var response = await client.PostAsync("/AutomatonGeneration/GenerateRandomAutomaton", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public void AutomatonGeneratorService_GenerateRandomPda_ShouldCreateValidStructure()
    {
        using var scope = GetServiceScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomatonGeneratorService>();

        var result = service.GenerateRandomAutomaton(AutomatonType.DPDA, 4, 8, 2, 0.5, 444);

        result.Type.ShouldBe(AutomatonType.DPDA);
        result.States.Count.ShouldBe(4);
        result.States.Count(s => s.IsStart).ShouldBe(1);
        result.Alphabet.Count.ShouldBe(2);
        result.IsCustomAutomaton.ShouldBeTrue();

        result.Transitions.ShouldNotBeEmpty();
    }

    [Fact]
    public void AutomatonGeneratorService_GenerateRandomPda_WithSeed_ShouldBeReproducible()
    {
        using var scope = GetServiceScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomatonGeneratorService>();

        var result1 = service.GenerateRandomAutomaton(AutomatonType.DPDA, 5, 10, 3, 0.4, 555);
        var result2 = service.GenerateRandomAutomaton(AutomatonType.DPDA, 5, 10, 3, 0.4, 555);

        result1.States.Count.ShouldBe(result2.States.Count);
        result1.Transitions.Count.ShouldBe(result2.Transitions.Count);
        result1.Alphabet.Count.ShouldBe(result2.Alphabet.Count);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    public async Task GenerateRandomPda_AllAcceptanceModes_ShouldWork(int acceptanceModeInt)
    {
        var client = GetHttpClient();
        var formData = new List<KeyValuePair<string, string>>
        {
            new("Type", "3"), // PDA
            new("StateCount", "4"),
            new("TransitionCount", "6"),
            new("AlphabetSize", "2"),
            new("AcceptingStateRatio", "0.5"),
            new("Seed", "666"),
            new("AcceptanceMode", acceptanceModeInt.ToString())
        };

        var response = await client.PostAsync("/AutomatonGeneration/GenerateRandomAutomaton", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("AUTOMATON");
    }

    [Fact]
    public void AutomatonGeneratorService_GenerateRandomPda_FinalStateOnly_ShouldSetMode()
    {
        using var scope = GetServiceScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomatonGeneratorService>();

        var result = service.GenerateRandomAutomaton(AutomatonType.DPDA, 3, 5, 2, 0.5, 777, PDAAcceptanceMode.FinalStateOnly);

        result.AcceptanceMode.ShouldBe(PDAAcceptanceMode.FinalStateOnly);
    }

    [Fact]
    public void AutomatonGeneratorService_GenerateRandomPda_EmptyStackOnly_ShouldSetMode()
    {
        using var scope = GetServiceScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomatonGeneratorService>();

        var result = service.GenerateRandomAutomaton(AutomatonType.DPDA, 3, 5, 2, 0.5, 888, PDAAcceptanceMode.EmptyStackOnly);

        result.AcceptanceMode.ShouldBe(PDAAcceptanceMode.EmptyStackOnly);
    }

    [Fact]
    public void AutomatonGeneratorService_GenerateRandomPda_FinalStateAndEmptyStack_ShouldSetMode()
    {
        using var scope = GetServiceScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomatonGeneratorService>();

        var result = service.GenerateRandomAutomaton(AutomatonType.DPDA, 3, 5, 2, 0.5, 999, PDAAcceptanceMode.FinalStateAndEmptyStack);

        result.AcceptanceMode.ShouldBe(PDAAcceptanceMode.FinalStateAndEmptyStack);
    }

    #endregion

    #region PDA Preset Tests

    [Fact]
    public void AutomatonPresetService_GetBalancedParenthesesPda_ShouldReturnWorkingPda()
    {
        using var scope = GetServiceScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomatonPresetService>();

        var result = service.GenerateBalancedParenthesesPda();

        result.Type.ShouldBe(AutomatonType.DPDA);
        result.States.ShouldNotBeEmpty();
        result.Transitions.ShouldNotBeEmpty();
        result.States.Count(s => s.IsStart).ShouldBe(1);
        result.AcceptanceMode.ShouldBeOneOf(Enum.GetValues<PDAAcceptanceMode>());
    }

    [Fact]
    public void AutomatonPresetService_GetAnBnPda_ShouldReturnWorkingPda()
    {
        using var scope = GetServiceScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomatonPresetService>();

        var result = service.GenerateAnBnPda();

        result.Type.ShouldBe(AutomatonType.DPDA);
        result.States.ShouldNotBeEmpty();
        result.Transitions.ShouldNotBeEmpty();
        result.AcceptanceMode.ShouldBeOneOf(Enum.GetValues<PDAAcceptanceMode>());
    }

    [Fact]
    public void AutomatonPresetService_GetPalindromePda_ShouldReturnWorkingPda()
    {
        using var scope = GetServiceScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomatonPresetService>();

        var result = service.GenerateEvenPalindromePda();

        result.Type.ShouldBe(AutomatonType.NPDA);
        result.States.ShouldNotBeEmpty();
        result.Transitions.ShouldNotBeEmpty();
        result.AcceptanceMode.ShouldBeOneOf(Enum.GetValues<PDAAcceptanceMode>());
    }

    [Fact]
    public void AutomatonPresetService_PdaPresetsHaveCorrectAcceptanceMode()
    {
        using var scope = GetServiceScope();
        var service = scope.ServiceProvider.GetRequiredService<IAutomatonPresetService>();


        var balancedParens = service.GenerateBalancedParenthesesPda();
        balancedParens.AcceptanceMode.ShouldBeOneOf(Enum.GetValues<PDAAcceptanceMode>());

        var anbn = service.GenerateAnBnPda();
        anbn.AcceptanceMode.ShouldBeOneOf(Enum.GetValues<PDAAcceptanceMode>());

        var palindrome = service.GenerateEvenPalindromePda();
        palindrome.AcceptanceMode.ShouldBeOneOf(Enum.GetValues<PDAAcceptanceMode>());
    }

    #endregion
}

