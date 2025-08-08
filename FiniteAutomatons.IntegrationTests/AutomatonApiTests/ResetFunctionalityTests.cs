using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;

namespace FiniteAutomatons.IntegrationTests.AutomatonApiTests;

[Collection("Integration Tests")]
public class ResetFunctionalityTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task Reset_ShouldPreserveAlphabetAndAutomatonStructure()
    {
        var client = GetHttpClient();

        // Create a simple DFA with alphabet
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
            Alphabet = ['a', 'b'],
            Input = "ababab",
            Position = 3,
            CurrentStateId = 2,
            Result = false,
            IsCustomAutomaton = true
        };

        // Test Reset action
        var response = await PostAutomatonForm(client, "/Automaton/Reset", dfaModel);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        
        // Verify alphabet is preserved - should still show 'a' and 'b' badges
        content.ShouldContain("<span class=\"badge bg-info me-2 mb-1\">a</span>");
        content.ShouldContain("<span class=\"badge bg-info me-2 mb-1\">b</span>");
        
        // Verify states are preserved
        content.ShouldContain("State 1");
        content.ShouldContain("State 2");
        
        // Verify transitions are preserved
        content.ShouldContain("1 --a--> 2");
        content.ShouldContain("2 --b--> 1");
        
        // Verify input is cleared (no value in input field)
        content.ShouldNotContain("value=\"ababab\"");
    }

    [Fact]
    public async Task Reset_WithEpsilonNFA_ShouldPreserveEpsilonTransitionsButNotInAlphabet()
    {
        var client = GetHttpClient();

        var epsilonModel = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0' }, // Epsilon
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
            ],
            Alphabet = ['a'],
            Input = "test",
            Position = 2,
            CurrentStates = [1, 2],
            IsCustomAutomaton = true
        };

        var response = await PostAutomatonForm(client, "/Automaton/Reset", epsilonModel);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        
        // Should show 'a' in alphabet
        content.ShouldContain("<span class=\"badge bg-info me-2 mb-1\">a</span>");
        
        // Should show epsilon transition
        content.ShouldContain("1 --?--> 2");
        content.ShouldContain("1 --a--> 2");
        
        // Input should be cleared
        content.ShouldNotContain("value=\"test\"");
    }

    [Fact]
    public async Task Reset_WithNoInput_ShouldStillWork()
    {
        var client = GetHttpClient();

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true }
            ],
            Transitions = [],
            Alphabet = [],
            Input = "",
            Position = 0,
            IsCustomAutomaton = true
        };

        var response = await PostAutomatonForm(client, "/Automaton/Reset", model);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("State 1");
        // Should not crash and should work normally
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