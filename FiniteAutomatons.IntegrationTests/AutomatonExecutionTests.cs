using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests;

/// <summary>
/// Integration tests for automaton execution functionality.
/// Tests the complete workflow: create automaton -> execute -> verify results.
/// </summary>
[Collection("Integration Tests")]
public class AutomatonExecutionTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task DFA_ExecuteAll_AcceptsValidInput()
    {
        // Arrange
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 1, Symbol = 'b' }
            ],
            Input = "a"
        };

        // Act
        var response = await PostAutomatonAsync(client, "/Automaton/ExecuteAll", model);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await DeserializeResponseAsync(response);
        result.IsAccepted.ShouldBe(true);
        result.CurrentStateId.ShouldBe(2);
    }

    [Fact]
    public async Task DFA_ExecuteAll_RejectsInvalidInput()
    {
        // Arrange
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 1, Symbol = 'b' }
            ],
            Input = "ab"
        };

        // Act
        var response = await PostAutomatonAsync(client, "/Automaton/ExecuteAll", model);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await DeserializeResponseAsync(response);
        result.IsAccepted.ShouldBe(false);
        result.CurrentStateId.ShouldBe(1);
    }

    [Fact]
    public async Task NFA_ExecuteAll_HandlesNondeterminism()
    {
        // Arrange
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' },
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
            ],
            Input = "a"
        };

        // Act
        var response = await PostAutomatonAsync(client, "/Automaton/ExecuteAll", model);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await DeserializeResponseAsync(response);
        result.IsAccepted.ShouldBe(true);
        result.CurrentStates.ShouldNotBeNull();
        result.CurrentStates.ShouldContain(2);
    }

    [Fact]
    public async Task EpsilonNFA_ExecuteAll_HandlesEpsilonTransitions()
    {
        // Arrange
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0' } // Epsilon
            ],
            Input = ""
        };

        // Act
        var response = await PostAutomatonAsync(client, "/Automaton/ExecuteAll", model);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await DeserializeResponseAsync(response);
        result.IsAccepted.ShouldBe(true);
    }

    [Fact]
    public async Task StepForward_IncreasesPosition()
    {
        // Arrange
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
            ],
            Input = "aa",
            Position = 0
        };

        // Act
        var response = await PostAutomatonAsync(client, "/Automaton/StepForward", model);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await DeserializeResponseAsync(response);
        result.Position.ShouldBe(1);
        result.CurrentStateId.ShouldBe(2);
    }

    [Fact]
    public async Task StepBackward_DecreasesPosition()
    {
        // Arrange - First execute to position 2, then step back
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 1, Symbol = 'b' }
            ],
            Input = "ab",
            Position = 0
        };

        // Execute all first to build state history
        var execResponse = await PostAutomatonAsync(client, "/Automaton/ExecuteAll", model);
        execResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var execResult = await DeserializeResponseAsync(execResponse);

        // Now step backward
        var backResponse = await PostAutomatonAsync(client, "/Automaton/StepBackward", execResult);

        // Assert
        backResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await DeserializeResponseAsync(backResponse);
        result.Position.ShouldBe(1);
    }

    [Fact]
    public async Task Reset_ClearsExecutionState()
    {
        // Arrange
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
            ],
            Input = "test",
            Position = 3,
            CurrentStateId = 2,
            HasExecuted = true
        };

        // Act
        var response = await PostAutomatonAsync(client, "/Automaton/Reset", model);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await DeserializeResponseAsync(response);
        result.Position.ShouldBe(0);
        result.CurrentStateId.ShouldBeNull();
        result.HasExecuted.ShouldBe(false);
        result.Input.ShouldBeEmpty();
    }

    [Fact]
    public async Task BackToStart_ResetsToInitialState()
    {
        // Arrange
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
            ],
            Input = "aaa",
            Position = 0
        };

        // First execute to move away from start
        var execResponse = await PostAutomatonAsync(client, "/Automaton/ExecuteAll", model);
        execResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var execResult = await DeserializeResponseAsync(execResponse);

        // Act - Go back to start
        var response = await PostAutomatonAsync(client, "/Automaton/BackToStart", execResult);

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await DeserializeResponseAsync(response);
        result.Position.ShouldBe(0);
        result.CurrentStateId.ShouldBe(1);
        result.Input.ShouldBe("aaa"); // Input preserved
    }

    // Helper methods
    private static async Task<HttpResponseMessage> PostAutomatonAsync(HttpClient client, string url, AutomatonViewModel model)
    {
        var formData = BuildFormData(model);
        var content = new FormUrlEncodedContent(formData);
        return await client.PostAsync(url, content);
    }

    private static List<KeyValuePair<string, string>> BuildFormData(AutomatonViewModel model)
    {
        var formData = new List<KeyValuePair<string, string>>
        {
            new("Type", ((int)model.Type).ToString()),
            new("Input", model.Input ?? ""),
            new("Position", model.Position.ToString()),
            new("HasExecuted", model.HasExecuted.ToString().ToLower()),
            new("IsCustomAutomaton", model.IsCustomAutomaton.ToString().ToLower()),
            new("StateHistorySerialized", model.StateHistorySerialized ?? "")
        };

        if (model.CurrentStateId.HasValue)
            formData.Add(new("CurrentStateId", model.CurrentStateId.Value.ToString()));

        if (model.CurrentStates != null)
        {
            var i = 0;
            foreach (var stateId in model.CurrentStates)
            {
                formData.Add(new($"CurrentStates.Index", i.ToString()));
                formData.Add(new($"CurrentStates[{i}]", stateId.ToString()));
                i++;
            }
        }

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

    private static async Task<AutomatonViewModel> DeserializeResponseAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync();
        
        var model = new AutomatonViewModel
        {
            States = new List<Core.Models.DoMain.State>(),
            Transitions = new List<Core.Models.DoMain.Transition>()
        };
        
        // Parse Type - more flexible pattern
        var typeMatch = Regex.Match(html, @"name\s*=\s*""Type""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""Type""", RegexOptions.IgnoreCase);
        if (typeMatch.Success)
            model.Type = (AutomatonType)int.Parse(typeMatch.Groups[1].Success ? typeMatch.Groups[1].Value : typeMatch.Groups[2].Value);

        // Parse Position - more flexible pattern
        var posMatch = Regex.Match(html, @"name\s*=\s*""Position""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""Position""", RegexOptions.IgnoreCase);
        if (posMatch.Success)
            model.Position = int.Parse(posMatch.Groups[1].Success ? posMatch.Groups[1].Value : posMatch.Groups[2].Value);

        // Parse CurrentStateId - more flexible pattern
        var stateMatch = Regex.Match(html, @"name\s*=\s*""CurrentStateId""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""CurrentStateId""", RegexOptions.IgnoreCase);
        if (stateMatch.Success)
            model.CurrentStateId = int.Parse(stateMatch.Groups[1].Success ? stateMatch.Groups[1].Value : stateMatch.Groups[2].Value);

        // Parse HasExecuted
        var hasExecMatch = Regex.Match(html, @"name\s*=\s*""HasExecuted""[^>]*value\s*=\s*""(true|false)""|value\s*=\s*""(true|false)""[^>]*name\s*=\s*""HasExecuted""", RegexOptions.IgnoreCase);
        if (hasExecMatch.Success)
            model.HasExecuted = bool.Parse(hasExecMatch.Groups[1].Success ? hasExecMatch.Groups[1].Value : hasExecMatch.Groups[2].Value);

        // Parse IsAccepted
        var acceptedMatch = Regex.Match(html, @"name\s*=\s*""IsAccepted""[^>]*value\s*=\s*""(true|false)""|value\s*=\s*""(true|false)""[^>]*name\s*=\s*""IsAccepted""", RegexOptions.IgnoreCase);
        if (acceptedMatch.Success)
            model.IsAccepted = bool.Parse(acceptedMatch.Groups[1].Success ? acceptedMatch.Groups[1].Value : acceptedMatch.Groups[2].Value);

        // Parse Input - look for the inputField
        var inputMatch = Regex.Match(html, @"id\s*=\s*""inputField""[^>]*value\s*=\s*""([^""]*)""|value\s*=\s*""([^""]*)""[^>]*id\s*=\s*""inputField""", RegexOptions.IgnoreCase);
        if (inputMatch.Success)
            model.Input = inputMatch.Groups[1].Success ? inputMatch.Groups[1].Value : inputMatch.Groups[2].Value;

        // Parse StateHistorySerialized
        var stateHistMatch = Regex.Match(html, @"name\s*=\s*""StateHistorySerialized""[^>]*value\s*=\s*""([^""]*)""|value\s*=\s*""([^""]*)""[^>]*name\s*=\s*""StateHistorySerialized""", RegexOptions.IgnoreCase);
        if (stateHistMatch.Success)
            model.StateHistorySerialized = stateHistMatch.Groups[1].Success ? stateHistMatch.Groups[1].Value : stateHistMatch.Groups[2].Value;

        // Parse CurrentStates (for NFA/EpsilonNFA)
        var currentStatesMatches = Regex.Matches(html, @"name\s*=\s*""CurrentStates\[\d+\]""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""CurrentStates\[\d+\]""", RegexOptions.IgnoreCase);
        if (currentStatesMatches.Count > 0)
        {
            model.CurrentStates = new HashSet<int>();
            foreach (Match match in currentStatesMatches)
            {
                var value = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[2].Value;
                model.CurrentStates.Add(int.Parse(value));
            }
        }

        // Parse States
        var stateIdMatches = Regex.Matches(html, @"name\s*=\s*""States\[(\d+)\]\.Id""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""States\[(\d+)\]\.Id""", RegexOptions.IgnoreCase);
        var stateStartMatches = Regex.Matches(html, @"name\s*=\s*""States\[\d+\]\.IsStart""[^>]*value\s*=\s*""(true|false)""|value\s*=\s*""(true|false)""[^>]*name\s*=\s*""States\[\d+\]\.IsStart""", RegexOptions.IgnoreCase);
        var stateAcceptMatches = Regex.Matches(html, @"name\s*=\s*""States\[\d+\]\.IsAccepting""[^>]*value\s*=\s*""(true|false)""|value\s*=\s*""(true|false)""[^>]*name\s*=\s*""States\[\d+\]\.IsAccepting""", RegexOptions.IgnoreCase);

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
        var transFromMatches = Regex.Matches(html, @"name\s*=\s*""Transitions\[\d+\]\.FromStateId""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""Transitions\[\d+\]\.FromStateId""", RegexOptions.IgnoreCase);
        var transToMatches = Regex.Matches(html, @"name\s*=\s*""Transitions\[\d+\]\.ToStateId""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""Transitions\[\d+\]\.ToStateId""", RegexOptions.IgnoreCase);
        var transSymbolMatches = Regex.Matches(html, @"name\s*=\s*""Transitions\[\d+\]\.Symbol""[^>]*value\s*=\s*""(.)""|value\s*=\s*""(.)""[^>]*name\s*=\s*""Transitions\[\d+\]\.Symbol""", RegexOptions.IgnoreCase);

        for (int i = 0; i < transFromMatches.Count && i < transToMatches.Count; i++)
        {
            var fromValue = transFromMatches[i].Groups[1].Success ? transFromMatches[i].Groups[1].Value : transFromMatches[i].Groups[2].Value;
            var toValue = transToMatches[i].Groups[1].Success ? transToMatches[i].Groups[1].Value : transToMatches[i].Groups[2].Value;
            char symbol = '\0';
            if (i < transSymbolMatches.Count)
            {
                var symbolValue = transSymbolMatches[i].Groups[1].Success ? transSymbolMatches[i].Groups[1].Value : transSymbolMatches[i].Groups[2].Value;
                if (!string.IsNullOrEmpty(symbolValue))
                    symbol = symbolValue[0];
            }
            
            model.Transitions.Add(new Core.Models.DoMain.Transition
            {
                FromStateId = int.Parse(fromValue),
                ToStateId = int.Parse(toValue),
                Symbol = symbol
            });
        }

        return model;
    }
}
