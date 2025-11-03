using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests;

/// <summary>
/// Integration tests for input field locking behavior.
/// Tests that input field is locked during execution and unlocked after reset.
/// </summary>
[Collection("Integration Tests")]
public class InputFieldLockingTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task InputField_WhenNotExecuted_ShouldNotBeDisabled()
    {
        // Arrange
        var client = GetHttpClient();

        // Act
        var response = await client.GetAsync("/");
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Input field should exist
        html.ShouldContain("id=\"inputField\"");

        // Should not be disabled or readonly when not executed
        var hasDisabledAttribute = Regex.IsMatch(html, @"id=""inputField""[^>]*disabled=""disabled""", RegexOptions.IgnoreCase);
        var hasReadonlyAttribute = Regex.IsMatch(html, @"id=""inputField""[^>]*readonly=""readonly""", RegexOptions.IgnoreCase);

        hasDisabledAttribute.ShouldBeFalse();
        hasReadonlyAttribute.ShouldBeFalse();
    }

    [Fact]
    public async Task InputField_AfterStart_ShouldBeDisabled()
    {
        // Arrange
        var client = GetHttpClient();
        var model = CreateSimpleDfaModel("ab");

        // Act - Start execution
        var response = await PostAutomatonAsync(client, "/Automaton/Start", model);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Input field should be disabled and readonly
        var inputFieldMatch = Regex.Match(html, @"<input[^>]*id=""inputField""[^>]*>", RegexOptions.IgnoreCase);
        inputFieldMatch.Success.ShouldBeTrue("Input field should exist");

        var inputTag = inputFieldMatch.Value;
        Regex.IsMatch(inputTag, @"disabled=""disabled""", RegexOptions.IgnoreCase).ShouldBeTrue("Input should be disabled");
        Regex.IsMatch(inputTag, @"readonly=""readonly""", RegexOptions.IgnoreCase).ShouldBeTrue("Input should be readonly");
    }

    [Fact]
    public async Task InputField_AfterStepForward_ShouldBeDisabled()
    {
        // Arrange
        var client = GetHttpClient();
        var model = CreateSimpleDfaModel("ab");

        // Act - Step forward (which sets HasExecuted)
        var response = await PostAutomatonAsync(client, "/Automaton/StepForward", model);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var inputFieldMatch = Regex.Match(html, @"<input[^>]*id=""inputField""[^>]*>", RegexOptions.IgnoreCase);
        inputFieldMatch.Success.ShouldBeTrue();

        var inputTag = inputFieldMatch.Value;
        Regex.IsMatch(inputTag, @"disabled=""disabled""", RegexOptions.IgnoreCase).ShouldBeTrue();
        Regex.IsMatch(inputTag, @"readonly=""readonly""", RegexOptions.IgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public async Task InputField_AfterExecuteAll_ShouldBeDisabled()
    {
        // Arrange
        var client = GetHttpClient();
        var model = CreateSimpleDfaModel("ab");

        // Act - Execute all
        var response = await PostAutomatonAsync(client, "/Automaton/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var inputFieldMatch = Regex.Match(html, @"<input[^>]*id=""inputField""[^>]*>", RegexOptions.IgnoreCase);
        inputFieldMatch.Success.ShouldBeTrue();

        var inputTag = inputFieldMatch.Value;
        Regex.IsMatch(inputTag, @"disabled=""disabled""", RegexOptions.IgnoreCase).ShouldBeTrue();
        Regex.IsMatch(inputTag, @"readonly=""readonly""", RegexOptions.IgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public async Task InputField_AfterReset_ShouldNotBeDisabled()
    {
        // Arrange
        var client = GetHttpClient();
        var model = CreateSimpleDfaModel("ab");

        // First execute to lock the input
        var execResponse = await PostAutomatonAsync(client, "/Automaton/Start", model);
        execResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var execResult = await DeserializeResponseAsync(execResponse);
        execResult.HasExecuted.ShouldBe(true);

        // Act - Reset
        var response = await PostAutomatonAsync(client, "/Automaton/Reset", execResult);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // After reset, input should be enabled
        var inputFieldMatch = Regex.Match(html, @"<input[^>]*id=""inputField""[^>]*>", RegexOptions.IgnoreCase);
        inputFieldMatch.Success.ShouldBeTrue();

        var inputTag = inputFieldMatch.Value;
        // Should NOT have disabled or readonly attributes set to true
        var hasDisabled = Regex.IsMatch(inputTag, @"disabled=""disabled""", RegexOptions.IgnoreCase);
        var hasReadonly = Regex.IsMatch(inputTag, @"readonly=""readonly""", RegexOptions.IgnoreCase);

        hasDisabled.ShouldBeFalse("Input field should not be disabled after reset");
        hasReadonly.ShouldBeFalse("Input field should not be readonly after reset");
    }

    [Fact]
    public async Task InputField_AfterStepBackwardToStart_ShouldNotBeDisabled()
    {
        // Arrange
        var client = GetHttpClient();
        var model = CreateSimpleDfaModel("a");

        // First Start to set HasExecuted
        var startResponse = await PostAutomatonAsync(client, "/Automaton/Start", model);
        startResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var startResult = await DeserializeResponseAsync(startResponse);
        startResult.HasExecuted.ShouldBe(true);

        // Execute one step forward
        var execResponse = await PostAutomatonAsync(client, "/Automaton/StepForward", startResult);
      execResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        
var execResult = await DeserializeResponseAsync(execResponse);
     execResult.HasExecuted.ShouldBe(true);
      execResult.Position.ShouldBe(1);

    // Act - Step back to start (position 0)
        var response = await PostAutomatonAsync(client, "/Automaton/StepBackward", execResult);
     var html = await response.Content.ReadAsStringAsync();

        // Assert
  response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
var result = await DeserializeResponseAsync(response);
        result.Position.ShouldBe(0);
        result.HasExecuted.ShouldBe(false, "HasExecuted should be false when stepping back to position 0");
        
    // Input field should be enabled again
  var inputFieldMatch = Regex.Match(html, @"<input[^>]*id=""inputField""[^>]*>", RegexOptions.IgnoreCase);
        inputFieldMatch.Success.ShouldBeTrue();
  
        var inputTag = inputFieldMatch.Value;
        var hasDisabled = Regex.IsMatch(inputTag, @"disabled=""disabled""", RegexOptions.IgnoreCase);
    var hasReadonly = Regex.IsMatch(inputTag, @"readonly=""readonly""", RegexOptions.IgnoreCase);
        
        hasDisabled.ShouldBeFalse("Input field should not be disabled when back at start");
 hasReadonly.ShouldBeFalse("Input field should not be readonly when back at start");
    }

    [Fact]
    public async Task InputField_AfterBackToStart_ShouldRemainDisabledIfHadExecuted()
    {
        // Arrange
        var client = GetHttpClient();
        var model = CreateSimpleDfaModel("ab");

        // Execute all
        var execResponse = await PostAutomatonAsync(client, "/Automaton/ExecuteAll", model);
        execResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var execResult = await DeserializeResponseAsync(execResponse);
        execResult.HasExecuted.ShouldBe(true);

        // Act - Back to start (preserves HasExecuted if execution had occurred)
        var response = await PostAutomatonAsync(client, "/Automaton/BackToStart", execResult);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var result = await DeserializeResponseAsync(response);
        // BackToStart should preserve HasExecuted
        result.HasExecuted.ShouldBe(true);

        // Input field should still be disabled
        var inputFieldMatch = Regex.Match(html, @"<input[^>]*id=""inputField""[^>]*>", RegexOptions.IgnoreCase);
        inputFieldMatch.Success.ShouldBeTrue();

        var inputTag = inputFieldMatch.Value;
        Regex.IsMatch(inputTag, @"disabled=""disabled""", RegexOptions.IgnoreCase).ShouldBeTrue();
        Regex.IsMatch(inputTag, @"readonly=""readonly""", RegexOptions.IgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public async Task InputField_MultipleExecutions_OnlyResetUnlocks()
    {
        // Arrange
        var client = GetHttpClient();
   var model = CreateSimpleDfaModel("abc");

        // Start execution first
    var startResponse = await PostAutomatonAsync(client, "/Automaton/Start", model);
      startResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
      VerifyInputIsLocked(await startResponse.Content.ReadAsStringAsync());

        // Act & Assert - Execute multiple times
        var result0 = await DeserializeResponseAsync(startResponse);
        var response1 = await PostAutomatonAsync(client, "/Automaton/StepForward", result0);
 response1.StatusCode.ShouldBe(HttpStatusCode.OK);
        VerifyInputIsLocked(await response1.Content.ReadAsStringAsync());

        var result1 = await DeserializeResponseAsync(response1);
        var response2 = await PostAutomatonAsync(client, "/Automaton/StepForward", result1);
   response2.StatusCode.ShouldBe(HttpStatusCode.OK);
  VerifyInputIsLocked(await response2.Content.ReadAsStringAsync());

        var result2 = await DeserializeResponseAsync(response2);
        var response3 = await PostAutomatonAsync(client, "/Automaton/StepBackward", result2);
 response3.StatusCode.ShouldBe(HttpStatusCode.OK);
  VerifyInputIsLocked(await response3.Content.ReadAsStringAsync());

        // Only reset should unlock
        var result3 = await DeserializeResponseAsync(response3);
        var resetResponse = await PostAutomatonAsync(client, "/Automaton/Reset", result3);
        resetResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        VerifyInputIsUnlocked(await resetResponse.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task InputValue_PreservedDuringExecution()
    {
        // Arrange
        var client = GetHttpClient();
        var inputValue = "testinput";
        var model = CreateSimpleDfaModel(inputValue);

        // Act - Execute
        var response = await PostAutomatonAsync(client, "/Automaton/Start", model);
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Input value should be preserved even when disabled
        var inputFieldMatch = Regex.Match(html, @"<input[^>]*id=""inputField""[^>]*value=""([^""]*)""[^>]*>", RegexOptions.IgnoreCase);
        inputFieldMatch.Success.ShouldBeTrue();

        var preservedValue = inputFieldMatch.Groups[1].Value;
        preservedValue.ShouldBe(inputValue);
    }

    // Helper methods
    private static AutomatonViewModel CreateSimpleDfaModel(string input)
    {
        return new AutomatonViewModel
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
            Input = input
        };
    }

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

        // Parse Input
        var inputMatch = Regex.Match(html, @"id\s*=\s*""inputField""[^>]*value\s*=\s*""([^""]*)""|value\s*=\s*""([^""]*)""[^>]*id\s*=\s*""inputField""", RegexOptions.IgnoreCase);
        if (inputMatch.Success)
            model.Input = inputMatch.Groups[1].Success ? inputMatch.Groups[1].Value : inputMatch.Groups[2].Value;

        // Parse StateHistorySerialized
        var stateHistMatch = Regex.Match(html, @"name\s*=\s*""StateHistorySerialized""[^>]*value\s*=\s*""([^""]*)""|value\s*=\s*""([^""]*)""[^>]*name\s*=\s*""StateHistorySerialized""", RegexOptions.IgnoreCase);
        if (stateHistMatch.Success)
            model.StateHistorySerialized = stateHistMatch.Groups[1].Success ? stateHistMatch.Groups[1].Value : stateHistMatch.Groups[2].Value;

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

    private static void VerifyInputIsLocked(string html)
    {
        var inputFieldMatch = Regex.Match(html, @"<input[^>]*id=""inputField""[^>]*>", RegexOptions.IgnoreCase);
        inputFieldMatch.Success.ShouldBeTrue("Input field should exist");

        var inputTag = inputFieldMatch.Value;
        Regex.IsMatch(inputTag, @"disabled=""disabled""", RegexOptions.IgnoreCase).ShouldBeTrue("Input should be disabled");
        Regex.IsMatch(inputTag, @"readonly=""readonly""", RegexOptions.IgnoreCase).ShouldBeTrue("Input should be readonly");
    }

    private static void VerifyInputIsUnlocked(string html)
    {
        var inputFieldMatch = Regex.Match(html, @"<input[^>]*id=""inputField""[^>]*>", RegexOptions.IgnoreCase);
        inputFieldMatch.Success.ShouldBeTrue("Input field should exist");

        var inputTag = inputFieldMatch.Value;
        var hasDisabled = Regex.IsMatch(inputTag, @"disabled=""disabled""", RegexOptions.IgnoreCase);
        var hasReadonly = Regex.IsMatch(inputTag, @"readonly=""readonly""", RegexOptions.IgnoreCase);

        hasDisabled.ShouldBeFalse("Input field should not be disabled");
        hasReadonly.ShouldBeFalse("Input field should not be readonly");
    }
}
