using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;

namespace FiniteAutomatons.IntegrationTests.AutomationFETests;

[Collection("Integration Tests")]
public class StartResetButtonVisibilityTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    private static AutomatonViewModel BuildSimpleDfa(string input, bool hasExecuted = false)
    {
        return new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
        [
        new() { Id =1, IsStart = true, IsAccepting = false },
 new() { Id =2, IsStart = false, IsAccepting = true }
        ],
            Transitions =
        [
        new() { FromStateId =1, ToStateId =2, Symbol = 'a' },
 new() { FromStateId =2, ToStateId =2, Symbol = 'a' }
        ],
            Input = input,
            HasExecuted = hasExecuted
        };
    }

    private static FormUrlEncodedContent ToFormContent(AutomatonViewModel model)
    {
        var data = new List<KeyValuePair<string, string>>
         {
         new("Type", ((int)model.Type).ToString()),
         new("Input", model.Input ?? string.Empty),
         new("HasExecuted", model.HasExecuted.ToString().ToLowerInvariant()),
         new("Position", model.Position.ToString()),
         new("IsCustomAutomaton", model.IsCustomAutomaton.ToString().ToLowerInvariant()),
         new("StateHistorySerialized", model.StateHistorySerialized ?? string.Empty)
         };

        if (model.CurrentStateId.HasValue)
            data.Add(new("CurrentStateId", model.CurrentStateId.Value.ToString()));

        if (model.CurrentStates != null)
        {
            int idx = 0;
            foreach (var cs in model.CurrentStates)
            {
                data.Add(new("CurrentStates.Index", idx.ToString()));
                data.Add(new($"CurrentStates[{idx}]", cs.ToString()));
                idx++;
            }
        }

        for (int i = 0; i < model.States.Count; i++)
        {
            data.Add(new("States.Index", i.ToString()));
            data.Add(new($"States[{i}].Id", model.States[i].Id.ToString()));
            data.Add(new($"States[{i}].IsStart", model.States[i].IsStart.ToString().ToLowerInvariant()));
            data.Add(new($"States[{i}].IsAccepting", model.States[i].IsAccepting.ToString().ToLowerInvariant()));
        }

        for (int i = 0; i < model.Transitions.Count; i++)
        {
            data.Add(new("Transitions.Index", i.ToString()));
            data.Add(new($"Transitions[{i}].FromStateId", model.Transitions[i].FromStateId.ToString()));
            data.Add(new($"Transitions[{i}].ToStateId", model.Transitions[i].ToStateId.ToString()));
            data.Add(new($"Transitions[{i}].Symbol", model.Transitions[i].Symbol.ToString()));
        }

        return new FormUrlEncodedContent(data);
    }

    [Fact]
    public async Task StartVisible_BeforeAnyExecution_ResetHidden()
    {
        // Arrange: simulate initial state by performing a Reset on a fresh model (HasExecuted false)
        var client = GetHttpClient();
        var model = BuildSimpleDfa("a", hasExecuted: false);
        var form = ToFormContent(model);

        // Act
        var response = await client.PostAsync("/Automaton/Reset", form);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        // Assert: Start button rendered, Reset button not rendered
        html.ShouldContain("title=\"Start\"");
        html.ShouldNotContain("title=\"Reset\"");
    }

    [Fact]
    public async Task AfterStepForward_ResetVisible_StartHidden()
    {
        // Arrange
        var client = GetHttpClient();
        var model = BuildSimpleDfa("a", hasExecuted: false);
        var form = ToFormContent(model);

        // Act: StepForward marks HasExecuted true and view should switch buttons
        var response = await client.PostAsync("/Automaton/StepForward", form);
        response.EnsureSuccessStatusCode();
        var html = await response.Content.ReadAsStringAsync();

        // Assert
        html.ShouldContain("title=\"Reset\"");
        html.ShouldNotContain("title=\"Start\"");
    }

    [Fact]
    public async Task AfterReset_Again_StartVisible_ResetHidden()
    {
        // Arrange: first execute then reset
        var client = GetHttpClient();
        var model = BuildSimpleDfa("aa", hasExecuted: false);
        var form = ToFormContent(model);
        var execResponse = await client.PostAsync("/Automaton/StepForward", form);
        execResponse.EnsureSuccessStatusCode();

        // Extract updated hidden fields for HasExecuted (we only need HasExecuted true to send to Reset)
        var executedHtml = await execResponse.Content.ReadAsStringAsync();
        // We don't parse fully; just send HasExecuted true in new form
        var afterExecModel = model; // reuse
        afterExecModel.HasExecuted = true; // ensure reset sees executed state
        var resetForm = ToFormContent(afterExecModel);

        // Act
        var resetResponse = await client.PostAsync("/Automaton/Reset", resetForm);
        resetResponse.EnsureSuccessStatusCode();
        var html = await resetResponse.Content.ReadAsStringAsync();

        // Assert
        html.ShouldContain("title=\"Start\"");
        html.ShouldNotContain("title=\"Reset\"");
    }

    [Fact]
    public async Task StepBackward_DoesNotRestoreStartButton_WhenHasExecutedTrue()
    {
        // Arrange: execute one step then step backward
        var client = GetHttpClient();
        var model = BuildSimpleDfa("aa", hasExecuted: false);
        var form = ToFormContent(model);
        var stepResponse = await client.PostAsync("/Automaton/StepForward", form);
        stepResponse.EnsureSuccessStatusCode();
        var stepHtml = await stepResponse.Content.ReadAsStringAsync();
        stepHtml.ShouldContain("title=\"Reset\"");

        // Build model with HasExecuted true for StepBackward
        model.HasExecuted = true;
        var backForm = ToFormContent(model);

        // Act
        var backResponse = await client.PostAsync("/Automaton/StepBackward", backForm);
        backResponse.EnsureSuccessStatusCode();
        var html = await backResponse.Content.ReadAsStringAsync();

        // Assert: still Reset visible, Start hidden
        html.ShouldContain("title=\"Reset\"");
        html.ShouldNotContain("title=\"Start\"");
    }
}
