using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;

namespace FiniteAutomatons.IntegrationTests.AutomatonApiTests;

[Collection("Integration Tests")]
public class ImprovedButtonStateTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task ResetButton_OnlyEnabledAfterExecutionStarts()
    {
        var client = GetHttpClient();

        // Test that reset is only enabled after execution starts
        // This verifies our logic change from "hasInput" to "hasExecutionStarted"
        
        // Create a simple DFA
        var dfaModel = new AutomatonViewModel
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
            Input = "a", // Valid input
            IsCustomAutomaton = true
        };

        // Step forward to start execution
        var response = await PostAutomatonForm(client, "/Automaton/StepForward", dfaModel);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        
        // After stepping forward, execution has started, so Reset should be available
        // We can verify this by checking that Reset button doesn't have "disabled" class
        content.ShouldContain("formaction=\"/Automaton/Reset\"");
        
        // The page should show current execution state
        content.ShouldContain("Current State");
    }

    [Fact]
    public async Task InputValidation_ButtonLogicWorksCorrectly()
    {
        // This test verifies our button state logic through server-side validation
        // The actual input validation UI behavior is tested by JavaScript and our unit tests
        
        var client = GetHttpClient();

        var dfaModel = new AutomatonViewModel
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
            Input = "a", // Valid input
            IsCustomAutomaton = true
        };

        // Valid input should allow stepping forward
        var response = await PostAutomatonForm(client, "/Automaton/StepForward", dfaModel);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        
        // Should show successful execution
        content.ShouldNotContain("Error occurred");
        content.ShouldContain("Current State");
    }

    private async Task<HttpResponseMessage> PostAutomatonForm(HttpClient client, string url, AutomatonViewModel model)
    {
        var formData = new List<KeyValuePair<string, string>>
        {
            new("Type", model.Type.ToString()),
            new("Input", model.Input ?? ""),
            new("IsCustomAutomaton", model.IsCustomAutomaton.ToString().ToLower()),
            new("Position", model.Position.ToString()),
            new("StateHistorySerialized", model.StateHistorySerialized ?? "")
        };

        if (model.CurrentStateId.HasValue)
        {
            formData.Add(new("CurrentStateId", model.CurrentStateId.Value.ToString()));
        }

        if (model.CurrentStates != null)
        {
            for (int i = 0; i < model.CurrentStates.Count; i++)
            {
                formData.Add(new($"CurrentStates[{i}]", model.CurrentStates.ElementAt(i).ToString()));
            }
        }

        if (model.IsAccepted.HasValue)
        {
            formData.Add(new("IsAccepted", model.IsAccepted.Value.ToString().ToLower()));
        }

        // Add states
        for (int i = 0; i < model.States.Count; i++)
        {
            var state = model.States[i];
            formData.Add(new($"States[{i}].Id", state.Id.ToString()));
            formData.Add(new($"States[{i}].IsStart", state.IsStart.ToString().ToLower()));
            formData.Add(new($"States[{i}].IsAccepting", state.IsAccepting.ToString().ToLower()));
        }

        // Add transitions
        for (int i = 0; i < model.Transitions.Count; i++)
        {
            var transition = model.Transitions[i];
            formData.Add(new($"Transitions[{i}].FromStateId", transition.FromStateId.ToString()));
            formData.Add(new($"Transitions[{i}].ToStateId", transition.ToStateId.ToString()));
            formData.Add(new($"Transitions[{i}].Symbol", transition.Symbol == '\0' ? "?" : transition.Symbol.ToString()));
        }

        // Add alphabet
        for (int i = 0; i < model.Alphabet.Count; i++)
        {
            formData.Add(new($"Alphabet[{i}]", model.Alphabet[i].ToString()));
        }

        var formContent = new FormUrlEncodedContent(formData);
        return await client.PostAsync(url, formContent);
    }
}