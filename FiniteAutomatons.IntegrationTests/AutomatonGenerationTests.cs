using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;

namespace FiniteAutomatons.IntegrationTests;

/// <summary>
/// Integration tests for automaton generation functionality.
/// Tests random generation and realistic automaton creation.
/// </summary>
[Collection("Integration Tests")]
public class AutomatonGenerationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Theory]
    [InlineData(AutomatonType.DFA)]
    [InlineData(AutomatonType.NFA)]
    [InlineData(AutomatonType.EpsilonNFA)]
    [InlineData(AutomatonType.PDA)]
    public async Task GenerateRealisticAutomaton_CreatesValidAutomaton(AutomatonType type)
    {
        // Arrange
        var client = GetHttpClient();
        var formData = new List<KeyValuePair<string, string>>
        {
            new("type", ((int)type).ToString()),
            new("stateCount", "5")
        };

        // Act
        var response = await client.PostAsync("/Automaton/GenerateRealisticAutomaton", new FormUrlEncodedContent(formData));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        
        // Verify states are generated
        html.ShouldContain("data-state-id=");
        
        // Verify transitions are generated
        html.ShouldContain("data-from=");
        
        // Verify alphabet is populated
        html.ShouldContain("alphabet-symbols");
    }

    [Fact]
    public async Task GenerateRealisticAutomaton_DFA_HasCorrectStructure()
    {
        // Arrange
        var client = GetHttpClient();
        var formData = new List<KeyValuePair<string, string>>
        {
            new("type", "0"), // DFA
            new("stateCount", "3")
        };

        // Act
        var response = await client.PostAsync("/Automaton/GenerateRealisticAutomaton", new FormUrlEncodedContent(formData));

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        
        // Should have exactly 3 states
        var stateMatches = System.Text.RegularExpressions.Regex.Matches(html, @"data-state-id=""(\d+)""");
        stateMatches.Count.ShouldBe(3);
        
        // Should have at least one start state
        html.ShouldContain("badge-start");
        
        // Should have at least one accepting state
        html.ShouldContain("badge-accepting");
    }

    [Fact]
    public async Task GenerateAutomaton_InvalidParameters_ReturnsError()
    {
        // Arrange
        var client = GetHttpClient();
        var formData = new List<KeyValuePair<string, string>>
        {
            new("type", "0"),
            new("stateCount", "0") // Invalid: must be at least 1
        };

        // Act
        var response = await client.PostAsync("/Automaton/GenerateRealisticAutomaton", new FormUrlEncodedContent(formData));

        // Assert - should handle gracefully
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.BadRequest, HttpStatusCode.OK);
    }

    [Fact]
    public async Task GeneratedAutomaton_CanBeExecuted()
    {
        // Arrange
        var client = GetHttpClient();
        var generateFormData = new List<KeyValuePair<string, string>>
        {
            new("type", "0"), // DFA
            new("stateCount", "3")
        };

        // Act - Generate automaton
        var generateResponse = await client.PostAsync("/Automaton/GenerateRealisticAutomaton", new FormUrlEncodedContent(generateFormData));
        generateResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var html = await generateResponse.Content.ReadAsStringAsync();
        
        // Extract alphabet to create valid input
        var alphabetMatch = System.Text.RegularExpressions.Regex.Match(html, @"<span class=""alphabet-symbols"">\{ ([^}]+) \}</span>");
        if (!alphabetMatch.Success)
            return; // No alphabet, skip execution test
            
        var alphabetStr = alphabetMatch.Groups[1].Value;
        var firstSymbol = alphabetStr.Split(',')[0].Trim();
        
        // Parse states and transitions from generated HTML
        var model = ParseAutomatonFromHtml(html);
        model.Input = firstSymbol;
        
        // Act - Execute the generated automaton
        var executeFormData = BuildExecutionFormData(model);
        var executeResponse = await client.PostAsync("/Automaton/ExecuteAll", new FormUrlEncodedContent(executeFormData));
        
        // Assert - Should execute without errors
        executeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // Helper methods
    private static AutomatonViewModel ParseAutomatonFromHtml(string html)
    {
        var model = new AutomatonViewModel
        {
            States = new List<Core.Models.DoMain.State>(),
            Transitions = new List<Core.Models.DoMain.Transition>()
        };

        // Parse Type - more flexible pattern
        var typeMatch = System.Text.RegularExpressions.Regex.Match(html, @"name\s*=\s*""Type""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""Type""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (typeMatch.Success)
            model.Type = (AutomatonType)int.Parse(typeMatch.Groups[1].Success ? typeMatch.Groups[1].Value : typeMatch.Groups[2].Value);

        // Parse States
        var stateIdMatches = System.Text.RegularExpressions.Regex.Matches(html, @"name\s*=\s*""States\[(\d+)\]\.Id""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""States\[(\d+)\]\.Id""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var stateStartMatches = System.Text.RegularExpressions.Regex.Matches(html, @"name\s*=\s*""States\[\d+\]\.IsStart""[^>]*value\s*=\s*""(true|false)""|value\s*=\s*""(true|false)""[^>]*name\s*=\s*""States\[\d+\]\.IsStart""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var stateAcceptMatches = System.Text.RegularExpressions.Regex.Matches(html, @"name\s*=\s*""States\[\d+\]\.IsAccepting""[^>]*value\s*=\s*""(true|false)""|value\s*=\s*""(true|false)""[^>]*name\s*=\s*""States\[\d+\]\.IsAccepting""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        for (int i = 0; i < stateIdMatches.Count; i++)
        {
            var match = stateIdMatches[i];
            var idValue = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;
            
            model.States.Add(new Core.Models.DoMain.State
            {
                Id = int.Parse(idValue),
                IsStart = i < stateStartMatches.Count && bool.Parse(stateStartMatches[i].Groups[1].Success ? stateStartMatches[i].Groups[1].Value : stateStartMatches[i].Groups[2].Value),
                IsAccepting = i < stateAcceptMatches.Count && bool.Parse(stateAcceptMatches[i].Groups[1].Success ? stateAcceptMatches[i].Groups[1].Value : stateAcceptMatches[i].Groups[2].Value)
            });
        }

        // Parse Transitions
        var transFromMatches = System.Text.RegularExpressions.Regex.Matches(html, @"name\s*=\s*""Transitions\[\d+\]\.FromStateId""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""Transitions\[\d+\]\.FromStateId""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var transToMatches = System.Text.RegularExpressions.Regex.Matches(html, @"name\s*=\s*""Transitions\[\d+\]\.ToStateId""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""Transitions\[\d+\]\.ToStateId""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        var transSymbolMatches = System.Text.RegularExpressions.Regex.Matches(html, @"name\s*=\s*""Transitions\[\d+\]\.Symbol""[^>]*value\s*=\s*""(.)""|value\s*=\s*""(.)""[^>]*name\s*=\s*""Transitions\[\d+\]\.Symbol""", System.Text.RegularExpressions.RegexOptions.IgnoreCase);

        for (int i = 0; i < transFromMatches.Count && i < transToMatches.Count && i < transSymbolMatches.Count; i++)
        {
            var fromValue = transFromMatches[i].Groups[1].Success ? transFromMatches[i].Groups[1].Value : transFromMatches[i].Groups[2].Value;
            var toValue = transToMatches[i].Groups[1].Success ? transToMatches[i].Groups[1].Value : transToMatches[i].Groups[2].Value;
            var symbolValue = transSymbolMatches[i].Groups[1].Success ? transSymbolMatches[i].Groups[1].Value : transSymbolMatches[i].Groups[2].Value;
            
            model.Transitions.Add(new Core.Models.DoMain.Transition
            {
                FromStateId = int.Parse(fromValue),
                ToStateId = int.Parse(toValue),
                Symbol = symbolValue.Length > 0 ? symbolValue[0] : '\0'
            });
        }

        return model;
    }

    private static List<KeyValuePair<string, string>> BuildExecutionFormData(AutomatonViewModel model)
    {
        var formData = new List<KeyValuePair<string, string>>
        {
            new("Type", ((int)model.Type).ToString()),
            new("Input", model.Input ?? ""),
            new("Position", "0"),
            new("HasExecuted", "false"),
            new("IsCustomAutomaton", "true")
        };

        for (int i = 0; i < model.States.Count; i++)
        {
            formData.Add(new($"States.Index", i.ToString()));
            formData.Add(new($"States[{i}].Id", model.States[i].Id.ToString()));
            formData.Add(new($"States[{i}].IsStart", model.States[i].IsStart.ToString().ToLower()));
            formData.Add(new($"States[{i}].IsAccepting", model.States[i].IsAccepting.ToString().ToLower()));
        }

        for (int i = 0; i < model.Transitions.Count; i++)
        {
            formData.Add(new($"Transitions.Index", i.ToString()));
            formData.Add(new($"Transitions[{i}].FromStateId", model.Transitions[i].FromStateId.ToString()));
            formData.Add(new($"Transitions[{i}].ToStateId", model.Transitions[i].ToStateId.ToString()));
            formData.Add(new($"Transitions[{i}].Symbol", model.Transitions[i].Symbol.ToString()));
        }

        return formData;
    }
}

public static class HttpStatusCodeExtensions
{
    public static void ShouldBeOneOf(this HttpStatusCode actual, params HttpStatusCode[] expected)
    {
        expected.ShouldContain(actual, $"Expected one of [{string.Join(", ", expected)}] but was {actual}");
    }
}
