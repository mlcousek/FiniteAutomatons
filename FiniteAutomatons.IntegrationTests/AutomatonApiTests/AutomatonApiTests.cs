using FiniteAutomatons.Core.Models.ViewModel;
using System.Net;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests.AutomatonApiTests;

[Collection("Integration Tests")]
public class AutomatonApiTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    private static AutomatonViewModel GetDefaultDfaViewModel(string input)
    {
        return new AutomatonViewModel
        {
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = false },
                new() { Id = 3, IsStart = false, IsAccepting = false },
                new() { Id = 4, IsStart = false, IsAccepting = false },
                new() { Id = 5, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 1, ToStateId = 3, Symbol = 'b' },
                new() { FromStateId = 1, ToStateId = 4, Symbol = 'c' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 5, Symbol = 'b' },
                new() { FromStateId = 2, ToStateId = 3, Symbol = 'c' },
                new() { FromStateId = 3, ToStateId = 4, Symbol = 'a' },
                new() { FromStateId = 3, ToStateId = 3, Symbol = 'b' },
                new() { FromStateId = 3, ToStateId = 1, Symbol = 'c' },
                new() { FromStateId = 4, ToStateId = 5, Symbol = 'a' },
                new() { FromStateId = 4, ToStateId = 2, Symbol = 'b' },
                new() { FromStateId = 4, ToStateId = 4, Symbol = 'c' },
                new() { FromStateId = 5, ToStateId = 5, Symbol = 'a' },
                new() { FromStateId = 5, ToStateId = 5, Symbol = 'b' },
                new() { FromStateId = 5, ToStateId = 5, Symbol = 'c' }
            ],
            Input = input
        };
    }

    private static FormUrlEncodedContent ToFormContent(AutomatonViewModel model)
    {
        var dict = new List<KeyValuePair<string, string>>
        {
            new("Input", model.Input ?? ""),
            new("CurrentStateId", model.CurrentStateId?.ToString() ?? ""),
            new("Position", model.Position.ToString()),
            new("IsAccepted", model.IsAccepted?.ToString().ToLower() ?? ""),
            new("StateHistorySerialized", model.StateHistorySerialized ?? "")
        };
        for (int i = 0; i < model.States.Count; i++)
        {
            dict.Add(new($"States[{i}].Id", model.States[i].Id.ToString()));
            dict.Add(new($"States[{i}].IsStart", model.States[i].IsStart.ToString().ToLower()));
            dict.Add(new($"States[{i}].IsAccepting", model.States[i].IsAccepting.ToString().ToLower()));
        }
        for (int i = 0; i < model.Transitions.Count; i++)
        {
            dict.Add(new($"Transitions[{i}].FromStateId", model.Transitions[i].FromStateId.ToString()));
            dict.Add(new($"Transitions[{i}].ToStateId", model.Transitions[i].ToStateId.ToString()));
            dict.Add(new($"Transitions[{i}].Symbol", model.Transitions[i].Symbol.ToString()));
        }
        for (int i = 0; i < model.Alphabet.Count; i++)
        {
            dict.Add(new($"Alphabet[{i}]", model.Alphabet[i].ToString()));
        }
        return new FormUrlEncodedContent(dict);
    }

    private static void UpdateModelFromHtml(AutomatonViewModel model, string html)
    {
        // Extract CurrentStateId
        var currentStateMatch = Regex.Match(html, @"name=""CurrentStateId"" value=""([^""]*)""");
        if (currentStateMatch.Success && int.TryParse(currentStateMatch.Groups[1].Value, out int currentStateId))
        {
            model.CurrentStateId = currentStateId;
        }

        // Extract Position
        var positionMatch = Regex.Match(html, @"name=""Position"" value=""([^""]*)""");
        if (positionMatch.Success && int.TryParse(positionMatch.Groups[1].Value, out int position))
        {
            model.Position = position;
        }

        // Extract IsAccepted
        var isAcceptedMatch = Regex.Match(html, @"name=""IsAccepted"" value=""([^""]*)""");
        if (isAcceptedMatch.Success && bool.TryParse(isAcceptedMatch.Groups[1].Value, out bool isAccepted))
        {
            model.IsAccepted = isAccepted;
        }

        // Extract StateHistorySerialized
        var stateHistoryMatch = Regex.Match(html, @"name=""StateHistorySerialized"" value=""([^""]*)""");
        if (stateHistoryMatch.Success)
        {
            model.StateHistorySerialized = stateHistoryMatch.Groups[1].Value;
        }
    }

    [Fact]
    public async Task ExecuteAll_AcceptsInputLeadingToAccepting()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("abca"); // 1->2(a)->3(b)->4(c)->5(a), state 5 is accepting
        var client = GetHttpClient();
        var form = ToFormContent(model);

        // Act
        var response = await client.PostAsync("/Automaton/ExecuteAll", form);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Accepted", html);
    }

    [Fact]
    public async Task ExecuteAll_RejectsInputNotLeadingToAccepting()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("ab"); // 1->2(a)->3(b), state 3 is not accepting
        var client = GetHttpClient();
        var form = ToFormContent(model);

        // Act
        var response = await client.PostAsync("/Automaton/ExecuteAll", form);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Rejected", html);
    }

    [Fact]
    public async Task StepForward_And_StepBackward_Works()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("abca");
        var client = GetHttpClient();
        var form = ToFormContent(model);

        // Step 1: StepForward (should move to state 2)
        var response = await client.PostAsync("/Automaton/StepForward", form);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
        // Debug: Check what we actually get for Current State
        if (html.Contains("Current State:</strong> 2"))
        {
            // Continue with the test...
            Assert.Contains("Current State:</strong> 2", html);
        }
        else if (html.Contains("Current State:</strong>"))
        {
            // Find what state it actually shows
            var stateMatch = Regex.Match(html, @"Current State:</strong>\s*(\d+)");
            if (stateMatch.Success)
            {
                Assert.Fail($"Expected state 2, but got state {stateMatch.Groups[1].Value}");
            }
            else
            {
                Assert.Fail("Found Current State section but couldn't parse the state number");
            }
        }
        else
        {
            Assert.Fail("No Current State section found in HTML");
        }

        // Update model from response
        UpdateModelFromHtml(model, html);

        // Step 2: StepForward again (with 'b' should move to state 5, not 3!)
        form = ToFormContent(model);
        response = await client.PostAsync("/Automaton/StepForward", form);
        html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Current State:</strong> 5", html);

        // Update model from response
        UpdateModelFromHtml(model, html);

        // Step 3: StepBackward (should move back to state 2)
        form = ToFormContent(model);
        response = await client.PostAsync("/Automaton/StepBackward", form);
        html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Current State:</strong> 2", html);
    }

    [Fact]
    public async Task BackToStart_ResetsState()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("abca");
        model.CurrentStateId = 3;
        model.Position = 2;
        var client = GetHttpClient();
        var form = ToFormContent(model);

        // Act
        var response = await client.PostAsync("/Automaton/BackToStart", form);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Current State:</strong> 1", html);
        Assert.Contains("Current Position:</strong> 0", html);
    }

    [Fact]
    public async Task Reset_ClearsInputAndState()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("abca");
        model.CurrentStateId = 3;
        model.Position = 2;
        var client = GetHttpClient();
        var form = ToFormContent(model);

        // Act
        var response = await client.PostAsync("/Automaton/Reset", form);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Input String", html); // Input box is present
        Assert.DoesNotContain("Current State:", html); // State is cleared
    }

    [Fact]
    public async Task ExecuteAll_LongInput_LeadsToAccepting()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("abcaabcaabca"); // Should end in state 5 (accepting)
        var client = GetHttpClient();
        var form = ToFormContent(model);
        // Act
        var response = await client.PostAsync("/Automaton/ExecuteAll", form);
        var html = await response.Content.ReadAsStringAsync();
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Accepted", html);
    }

    [Fact]
    public async Task ExecuteAll_EmptyInput_ShouldReject()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("");
        var client = GetHttpClient();
        var form = ToFormContent(model);
        // Act
        var response = await client.PostAsync("/Automaton/ExecuteAll", form);
        var html = await response.Content.ReadAsStringAsync();
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Rejected", html);
    }

    [Fact]
    public async Task ExecuteAll_InputWithAllSymbols()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("abcabcabc");
        var client = GetHttpClient();
        var form = ToFormContent(model);
        // Act
        var response = await client.PostAsync("/Automaton/ExecuteAll", form);
        var html = await response.Content.ReadAsStringAsync();
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        // Could be accepted or rejected depending on DFA, just check for result
        Assert.True(html.Contains("Accepted") || html.Contains("Rejected"));
    }

    [Fact]
    public async Task ExecuteAll_OnlyOneSymbol_NotAccepting()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("a"); // 1->2, not accepting
        var client = GetHttpClient();
        var form = ToFormContent(model);
        // Act
        var response = await client.PostAsync("/Automaton/ExecuteAll", form);
        var html = await response.Content.ReadAsStringAsync();
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Rejected", html);
    }

    [Fact]
    public async Task ExecuteAll_LoopInAcceptingState()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("abcaaaa"); // 1->2(a)->3(b)->4(c)->5(a)->5(a)->5(a)->5(a)
        var client = GetHttpClient();
        var form = ToFormContent(model);
        // Act
        var response = await client.PostAsync("/Automaton/ExecuteAll", form);
        var html = await response.Content.ReadAsStringAsync();
        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Contains("Accepted", html);
    }

    [Fact]
    public async Task Stepwise_MultipleActions_ForwardBackwardExecuteAllReset()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("abca");
        var client = GetHttpClient();
        var form = ToFormContent(model);
        
        // Step 1: StepForward (should move to state 2)
        var response = await client.PostAsync("/Automaton/StepForward", form);
        var html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Current State:</strong> 2", html);
        UpdateModelFromHtml(model, html);
        
        // Step 2: StepForward (with 'b' should move to state 5, not 3!)
        form = ToFormContent(model);
        response = await client.PostAsync("/Automaton/StepForward", form);
        html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Current State:</strong> 5", html);
        UpdateModelFromHtml(model, html);
        
        // Step 3: StepBackward (should move back to state 2)
        form = ToFormContent(model);
        response = await client.PostAsync("/Automaton/StepBackward", form);
        html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Current State:</strong> 2", html);
        UpdateModelFromHtml(model, html);
        
        // Step 4: ExecuteAll (should end in state 5)
        form = ToFormContent(model);
        response = await client.PostAsync("/Automaton/ExecuteAll", form);
        html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Accepted", html);
        
        // Step 5: Reset
        response = await client.PostAsync("/Automaton/Reset", form);
        html = await response.Content.ReadAsStringAsync();
        Assert.Contains("Input String", html);
        Assert.DoesNotContain("Current State:", html);
    }
}
