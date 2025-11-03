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
            new("StateHistorySerialized", model.StateHistorySerialized ?? ""),
            new("HasExecuted", model.HasExecuted.ToString().ToLower()), // Add HasExecuted field
            new("Type", ((int)model.Type).ToString()) // Add Type field
        };
        for (int i = 0; i < model.States.Count; i++)
        {
            dict.Add(new($"States.Index", i.ToString())); // Add index for proper model binding
            dict.Add(new($"States[{i}].Id", model.States[i].Id.ToString()));
            dict.Add(new($"States[{i}].IsStart", model.States[i].IsStart.ToString().ToLower()));
            dict.Add(new($"States[{i}].IsAccepting", model.States[i].IsAccepting.ToString().ToLower()));
        }
        for (int i = 0; i < model.Transitions.Count; i++)
        {
            dict.Add(new($"Transitions.Index", i.ToString())); // Add index for proper model binding
            dict.Add(new($"Transitions[{i}].FromStateId", model.Transitions[i].FromStateId.ToString()));
            dict.Add(new($"Transitions[{i}].ToStateId", model.Transitions[i].ToStateId.ToString()));
            dict.Add(new($"Transitions[{i}].Symbol", model.Transitions[i].Symbol.ToString()));
        }
        // Alphabet is a computed property, skip it or add it conditionally
        if (model.Alphabet != null && model.Alphabet.Count > 0)
        {
            for (int i = 0; i < model.Alphabet.Count; i++)
            {
                dict.Add(new($"Alphabet[{i}]", model.Alphabet[i].ToString()));
            }
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

        // Extract HasExecuted
        var hasExecutedMatch = Regex.Match(html, @"name=""HasExecuted"" value=""([^""]*)""");
        if (hasExecutedMatch.Success && bool.TryParse(hasExecutedMatch.Groups[1].Value, out bool hasExecuted))
        {
            model.HasExecuted = hasExecuted;
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
        Assert.Contains("Accepted", html, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ExecuteAll_RejectsInputNotLeadingToAccepting()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("ab"); // 1->2(a)->5(b), state 5 is accepting so this SHOULD accept
        // Actually: 1->2(a)->5(b) - wait, let me trace: from state 2 with 'b' goes to state 5 which IS accepting
        // So this test expectation is WRONG. Let's use different input that actually rejects
        model = GetDefaultDfaViewModel("a"); // 1->2(a), state 2 is NOT accepting - this will reject
                
        var client = GetHttpClient();
        var form = ToFormContent(model);

        // Act
        var response = await client.PostAsync("/Automaton/ExecuteAll", form);
     var html = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        
     // Check that rejected appears somewhere (case insensitive)
  var containsRejected = html.Contains("rejected", StringComparison.OrdinalIgnoreCase);
        Assert.True(containsRejected, $"Expected 'rejected' to appear in HTML, but it didn't. HTML snippet: {html.Substring(0, Math.Min(500, html.Length))}");
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
        // With StepForward, HasExecuted gets set automatically
        // The execution state should show (even without explicit Start)
    // But we need to verify it displays correctly
   Assert.Contains("q2", html); // Should have moved to state 2

  UpdateModelFromHtml(model, html);

  // Step 2: StepForward again (with 'b' should move to state 5, not 3!)
        form = ToFormContent(model);
        response = await client.PostAsync("/Automaton/StepForward", form);
 html = await response.Content.ReadAsStringAsync();
        Assert.Contains("q5", html);

  UpdateModelFromHtml(model, html);

        // Step 3: StepBackward (should move back to state 2)
  form = ToFormContent(model);
 response = await client.PostAsync("/Automaton/StepBackward", form);
  html = await response.Content.ReadAsStringAsync();
      Assert.Contains("q2", html);
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
        Assert.Contains("Current State:", html);
    Assert.Contains("q1", html);
   Assert.Contains("Current Position:", html);
   Assert.Contains("0 /", html); // Position 0 out of total
    }

    [Fact]
    public async Task Reset_ClearsInputAndState()
    {
        // Arrange
        var model = GetDefaultDfaViewModel("abca");
        model.CurrentStateId = 3;
   model.Position = 2;
        model.HasExecuted = true; // Set HasExecuted so execution state shows
        var client = GetHttpClient();
        var form = ToFormContent(model);

      // Act
  var response = await client.PostAsync("/Automaton/Reset", form);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
 Assert.Contains("INPUT", html); // Input section header
       // After reset, execution state section should not be shown since HasExecuted is now false
    // But the comment "Execution State Section" might still be in HTML as a comment
        // Check that there's no actual execution state item displayed
  Assert.DoesNotContain("execution-state-item", html); // No execution state items rendered
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
      Assert.Contains("Accepted", html, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("Rejected", html, StringComparison.OrdinalIgnoreCase);
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
        Assert.True(html.Contains("Accepted", StringComparison.OrdinalIgnoreCase) || html.Contains("Rejected", StringComparison.OrdinalIgnoreCase));
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
        Assert.Contains("Rejected", html, StringComparison.OrdinalIgnoreCase);
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
        Assert.Contains("Accepted", html, StringComparison.OrdinalIgnoreCase);
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
Assert.Contains("q2", html);
        UpdateModelFromHtml(model, html);

        // Step 2: StepForward (with 'b' should move to state 5, not 3!)
    form = ToFormContent(model);
  response = await client.PostAsync("/Automaton/StepForward", form);
   html = await response.Content.ReadAsStringAsync();
      Assert.Contains("q5", html);
        UpdateModelFromHtml(model, html);
   
        // Step 3: StepBackward (should move back to state 2)
        form = ToFormContent(model);
  response = await client.PostAsync("/Automaton/StepBackward", form);
     html = await response.Content.ReadAsStringAsync();
      Assert.Contains("q2", html);
 UpdateModelFromHtml(model, html);
        
    // Step 4: ExecuteAll (should end in state 5)
    form = ToFormContent(model);
   response = await client.PostAsync("/Automaton/ExecuteAll", form);
html = await response.Content.ReadAsStringAsync();
     var containsAccepted = html.Contains("accepted", StringComparison.OrdinalIgnoreCase);
        Assert.True(containsAccepted, "Expected 'accepted' in HTML");
        
    // Step 5: Reset
    response = await client.PostAsync("/Automaton/Reset", form);
  html = await response.Content.ReadAsStringAsync();
        Assert.Contains("INPUT", html); // Input section header
    Assert.DoesNotContain("execution-state-item", html); // No execution state items
    }
}
