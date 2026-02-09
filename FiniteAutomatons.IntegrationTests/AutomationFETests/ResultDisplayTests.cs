using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests.AutomationFETests;

/// <summary>
/// Integration tests for result display (ACCEPTED/REJECTED) after full input is read.
/// </summary>
[Collection("Integration Tests")]
public class ResultDisplayTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task ResultDisplay_AfterExecuteAll_Accepted_ShowsAcceptedBadge()
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
            Input = "a"
        };

        // Act
        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Should show Result section with ACCEPTED
        html.ShouldContain("Result:");
        html.ShouldContain("ACCEPTED", Case.Insensitive);

        // Should have result-accepted class
        var hasAcceptedBadge = Regex.IsMatch(html, @"result-badge.*result-accepted", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        hasAcceptedBadge.ShouldBeTrue("Should display accepted badge");
    }

    [Fact]
    public async Task ResultDisplay_AfterExecuteAll_Rejected_ShowsRejectedBadge()
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
            Input = "ab" // Will be rejected
        };

        // Act
        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Should show Result section with REJECTED
        html.ShouldContain("Result:");
        html.ShouldContain("REJECTED", Case.Insensitive);

        // Should have result-rejected class
        var hasRejectedBadge = Regex.IsMatch(html, @"result-badge.*result-rejected", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        hasRejectedBadge.ShouldBeTrue("Should display rejected badge");
    }

    [Fact]
    public async Task ResultDisplay_BeforeFullInputRead_DoesNotShowResult()
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

        // Act - Only step once, not reading full input
        var startResponse = await PostAutomatonAsync(client, "/AutomatonExecution/Start", model);
        var result = await DeserializeResponseAsync(startResponse);

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/StepForward", result);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var parsedResult = await DeserializeResponseAsync(response);
        parsedResult.Position.ShouldBeLessThan(parsedResult.Input!.Length);

        // Should NOT show Result badge yet (input not fully read)
        var hasResultBadge = Regex.IsMatch(html, @"<span[^>]*class=""[^""]*result-badge", RegexOptions.IgnoreCase);
        hasResultBadge.ShouldBeFalse("Should not display result badge until input is fully read");
    }

    [Fact]
    public async Task ResultDisplay_EmptyInput_Accepted_ShowsAccepted()
    {
        // Arrange
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
         [
 new() { Id = 1, IsStart = true, IsAccepting = true } // Start state is accepting
       ],
            Transitions = [],
            Input = "" // Empty input
        };

        // Act
        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await DeserializeResponseAsync(response);
        result.Position.ShouldBe(0);
        result.IsAccepted.ShouldBe(true);

        // Should show ACCEPTED badge
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task ResultDisplay_NFA_Accepted_ShowsAccepted()
    {
        // Arrange
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States =
            [
             new() { Id = 1, IsStart = true, IsAccepting = false },
        new() { Id = 2, IsStart = false, IsAccepting = true },
         new() { Id = 3, IsStart = false, IsAccepting = false }
            ],
            Transitions =
   [
      new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
        new() { FromStateId = 1, ToStateId = 3, Symbol = 'a' } // Non-deterministic
   ],
            Input = "a"
        };

        // Act
        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Should show ACCEPTED badge (at least one path leads to accepting state)
        html.ShouldContain("Result:");
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task ResultDisplay_AfterBackToStart_DoesNotShowResultIfNotAtEnd()
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
            Input = "a"
        };

        // Execute all first
        var execResponse = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var execResult = await DeserializeResponseAsync(execResponse);

        // Act - Back to start
        var response = await PostAutomatonAsync(client, "/AutomatonExecution/BackToStart", execResult);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await DeserializeResponseAsync(response);
        result.Position.ShouldBe(0);

        // Position is 0, input length is 1, so result badge should not show
        // (Only shows when position >= input.length AND IsAccepted.HasValue)
        var hasResultBadge = Regex.IsMatch(html, @"result-badge", RegexOptions.IgnoreCase);
        hasResultBadge.ShouldBeFalse("Should not show result badge at start position");
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
            new("StateHistorySerialized", model.StateHistorySerialized ?? ""),
            new("AcceptanceMode", ((int)model.AcceptanceMode).ToString())
        };

        if (model.CurrentStateId.HasValue)
            formData.Add(new("CurrentStateId", model.CurrentStateId.Value.ToString()));

        if (model.IsAccepted.HasValue)
            formData.Add(new("IsAccepted", model.IsAccepted.Value.ToString().ToLower()));

        if (!string.IsNullOrEmpty(model.StackSerialized))
            formData.Add(new("StackSerialized", model.StackSerialized));

        if (!string.IsNullOrEmpty(model.InitialStackSerialized))
            formData.Add(new("InitialStackSerialized", model.InitialStackSerialized));

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
            // Serialize '\0' as "\\0" for epsilon transitions, otherwise use ToString()
            formData.Add(new($"Transitions[{i}].Symbol", model.Transitions[i].Symbol == '\0' ? "\\0" : model.Transitions[i].Symbol.ToString()));
            if (model.Transitions[i].StackPop.HasValue)
            {
                // Serialize '\0' as "\\0" for epsilon stack pop, otherwise use ToString()
                var stackPopValue = model.Transitions[i].StackPop!.Value == '\0' ? "\\0" : model.Transitions[i].StackPop!.Value.ToString();
                formData.Add(new($"Transitions[{i}].StackPop", stackPopValue));
            }
            if (!string.IsNullOrEmpty(model.Transitions[i].StackPush))
                formData.Add(new($"Transitions[{i}].StackPush", model.Transitions[i].StackPush ?? ""));
        }

        return formData;
    }

    private static async Task<AutomatonViewModel> DeserializeResponseAsync(HttpResponseMessage response)
    {
        var html = await response.Content.ReadAsStringAsync();

        var model = new AutomatonViewModel
        {
            States = [],
            Transitions = []
        };

        // Parse Type
        var typeMatch = Regex.Match(html, @"name\s*=\s*""Type""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""Type""", RegexOptions.IgnoreCase);
        if (typeMatch.Success)
            model.Type = (AutomatonType)int.Parse(typeMatch.Groups[1].Success ? typeMatch.Groups[1].Value : typeMatch.Groups[2].Value);

        // Parse Position
        var posMatch = Regex.Match(html, @"name\s*=\s*""Position""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""Position""", RegexOptions.IgnoreCase);
        if (posMatch.Success)
            model.Position = int.Parse(posMatch.Groups[1].Success ? posMatch.Groups[1].Value : posMatch.Groups[2].Value);

        // Parse CurrentStateId
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

        // Parse Input
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

        // Deduplicate by index (HTML contains multiple forms with same state indices)
        var processedIndices = new HashSet<int>();
        for (int i = 0; i < stateIdMatches.Count; i++)
        {
            var match = stateIdMatches[i];
            var indexValue = match.Groups[1].Success ? match.Groups[1].Value : match.Groups[4].Value;
            int index = int.Parse(indexValue);
            if (processedIndices.Contains(index)) continue; // Skip duplicates
            processedIndices.Add(index);
            
            var idValue = match.Groups[2].Success ? match.Groups[2].Value : match.Groups[3].Value;

            model.States.Add(new Core.Models.DoMain.State
            {
                Id = int.Parse(idValue),
                IsStart = i < stateStartMatches.Count && bool.Parse(stateStartMatches[i].Groups[1].Success ? stateStartMatches[i].Groups[1].Value : stateStartMatches[i].Groups[2].Value),
                IsAccepting = i < stateAcceptMatches.Count && bool.Parse(stateAcceptMatches[i].Groups[1].Success ? stateAcceptMatches[i].Groups[1].Value : stateAcceptMatches[i].Groups[2].Value)
            });
        }

        // Parse Transitions
        var transFromMatches = Regex.Matches(html, @"name\s*=\s*""Transitions\[(\d+)\]\.FromStateId""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""Transitions\[(\d+)\]\.FromStateId""", RegexOptions.IgnoreCase);
        var transToMatches = Regex.Matches(html, @"name\s*=\s*""Transitions\[\d+\]\.ToStateId""[^>]*value\s*=\s*""(\d+)""|value\s*=\s*""(\d+)""[^>]*name\s*=\s*""Transitions\[\d+\]\.ToStateId""", RegexOptions.IgnoreCase);
        var transSymbolMatches = Regex.Matches(html, @"name\s*=\s*""Transitions\[\d+\]\.Symbol""[^>]*value\s*=\s*""(.)""|value\s*=\s*""(.)""[^>]*name\s*=\s*""Transitions\[\d+\]\.Symbol""", RegexOptions.IgnoreCase);

        // Deduplicate by index (HTML contains multiple forms with same transition indices)
        processedIndices.Clear();
        for (int i = 0; i < transFromMatches.Count && i < transToMatches.Count; i++)
        {
            var fromMatch = transFromMatches[i];
            var indexValue = fromMatch.Groups[1].Success ? fromMatch.Groups[1].Value : fromMatch.Groups[4].Value;
            int index = int.Parse(indexValue);
            if (processedIndices.Contains(index)) continue; // Skip duplicates
            processedIndices.Add(index);
            
            var fromValue = fromMatch.Groups[2].Success ? fromMatch.Groups[2].Value : fromMatch.Groups[3].Value;
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

    #region PDA Result Display Tests

    private static AutomatonViewModel BuildBalancedParenthesesPda(string input, PDAAcceptanceMode? acceptanceMode = null) => new()
    {
        Type = AutomatonType.PDA,
        States =
        [
            new() { Id = 1, IsStart = true, IsAccepting = true }
        ],
        Transitions =
        [
            new() { FromStateId = 1, ToStateId = 1, Symbol = '(', StackPop = '\0', StackPush = "(" },
            new() { FromStateId = 1, ToStateId = 1, Symbol = ')', StackPop = '(', StackPush = null }
        ],
        Input = input,
        IsCustomAutomaton = true,
        AcceptanceMode = acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack
    };

    private static AutomatonViewModel BuildAnBnPda(string input, PDAAcceptanceMode? acceptanceMode = null) => new()
    {
        Type = AutomatonType.PDA,
        States =
        [
            new() { Id = 1, IsStart = true, IsAccepting = false },
            new() { Id = 2, IsStart = false, IsAccepting = true }
        ],
        Transitions =
        [
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" },
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = null },
            new() { FromStateId = 1, ToStateId = 2, Symbol = '\0', StackPop = '\0', StackPush = null }
        ],
        Input = input,
        IsCustomAutomaton = true,
        AcceptanceMode = acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack
    };

    [Fact]
    public async Task Pda_ResultDisplay_BalancedParentheses_Accepted_ShowsAcceptedBadge()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("(())");

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("Result:");
        html.ShouldContain("ACCEPTED", Case.Insensitive);

        var hasAcceptedBadge = Regex.IsMatch(html, @"result-badge.*result-accepted", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        hasAcceptedBadge.ShouldBeTrue("Should display accepted badge");
    }

    [Fact]
    public async Task Pda_ResultDisplay_UnbalancedParentheses_Rejected_ShowsRejectedBadge()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("(()");

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("Result:");
        html.ShouldContain("REJECTED", Case.Insensitive);

        var hasRejectedBadge = Regex.IsMatch(html, @"result-badge.*result-rejected", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        hasRejectedBadge.ShouldBeTrue("Should display rejected badge");
    }

    [Fact]
    public async Task Pda_ResultDisplay_AnBn_Valid_ShowsAccepted()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda("aaabbb");

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_AnBn_WrongCount_ShowsRejected()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda("aaabb");

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("REJECTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_BeforeFullInputRead_DoesNotShowResult()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()()");

        var startResponse = await PostAutomatonAsync(client, "/AutomatonExecution/Start", model);
        var result = await DeserializeResponseAsync(startResponse);

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/StepForward", result);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var parsedResult = await DeserializeResponseAsync(response);
        parsedResult.Position.ShouldBeLessThan(parsedResult.Input!.Length);

        var hasResultBadge = Regex.IsMatch(html, @"<span[^>]*class=""[^""]*result-badge", RegexOptions.IgnoreCase);
        hasResultBadge.ShouldBeFalse("Should not display result badge until input is fully read");
    }

    [Fact]
    public async Task Pda_ResultDisplay_EmptyInput_AcceptingStart_ShowsAccepted()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = "",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await DeserializeResponseAsync(response);
        result.Position.ShouldBe(0);
        result.IsAccepted.ShouldBe(true);

        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_FinalStateOnly_AcceptWithNonEmptyStack_ShowsAccepted()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" }],
            Input = "a",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_FinalStateOnly_NonAcceptingState_ShowsRejected()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions = [],
            Input = "",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("REJECTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_EmptyStackOnly_EmptyStackAnyState_ShowsAccepted()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = null }
            ],
            Input = "ab",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly
        };

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_EmptyStackOnly_NonEmptyStack_ShowsRejected()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" }],
            Input = "a",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly
        };

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("REJECTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_FinalStateAndEmptyStack_BothConditionsMet_ShowsAccepted()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()", PDAAcceptanceMode.FinalStateAndEmptyStack);

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_FinalStateAndEmptyStack_OnlyFinalState_ShowsRejected()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" }],
            Input = "a",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("REJECTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_FinalStateAndEmptyStack_OnlyEmptyStack_ShowsRejected()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = null }
            ],
            Input = "ab",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("REJECTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_AfterBackToStart_DoesNotShowResult()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");

        var execResponse = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var execResult = await DeserializeResponseAsync(execResponse);

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/BackToStart", execResult);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await DeserializeResponseAsync(response);
        result.Position.ShouldBe(0);

        var hasResultBadge = Regex.IsMatch(html, @"result-badge", RegexOptions.IgnoreCase);
        hasResultBadge.ShouldBeFalse("Should not show result badge at start position");
    }

    [Fact]
    public async Task Pda_ResultDisplay_EpsilonClosureToAccepting_ShowsAccepted()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = '\0', StackPop = '\0', StackPush = null }],
            Input = "",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_LongValidInput_ShowsAccepted()
    {
        var client = GetHttpClient();
        var longInput = string.Concat(Enumerable.Repeat("()", 20));
        var model = BuildBalancedParenthesesPda(longInput);

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_DeepNesting_Accepted_ShowsAccepted()
    {
        var client = GetHttpClient();
        var deep = string.Concat(Enumerable.Repeat("(", 15)) + string.Concat(Enumerable.Repeat(")", 15));
        var model = BuildBalancedParenthesesPda(deep);

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_MultipleSymbols_AnBn_ShowsCorrect()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda("aaaaabbbb");

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("REJECTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_EmptyInputEmptyStackOnly_ShowsAccepted()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions = [],
            Input = "",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly
        };

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_ComplexRejection_ShowsRejectedBadge()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("())(");

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("REJECTED", Case.Insensitive);

        // Since execution stops at position 2 (out of 4), the Result badge won't show.
        // Instead, check for acceptance-status rejected in the "Is Accepted" section
        var hasRejectedStatus = Regex.IsMatch(html, @"acceptance-status.*rejected", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        hasRejectedStatus.ShouldBeTrue("Should display rejected status");
    }

    [Fact]
    public async Task Pda_ResultDisplay_AlternatingPatterns_Accepted()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()()()");

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_SinglePair_Accepted()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda("ab");

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_OnlyOpenParens_Rejected()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("(((");

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("REJECTED", Case.Insensitive);
    }

    [Fact]
    public async Task Pda_ResultDisplay_OnlyCloseParens_Rejected()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda(")))");

        var response = await PostAutomatonAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldContain("REJECTED", Case.Insensitive);
    }

    #endregion
}

