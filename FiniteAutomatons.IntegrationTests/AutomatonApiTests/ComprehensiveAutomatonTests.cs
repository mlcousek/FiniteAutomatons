using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;

namespace FiniteAutomatons.IntegrationTests.AutomatonApiTests;

public static class TestExtensions
{
    public static void ShouldBeOneOf<T>(this T actual, params T[] expected)
    {
        expected.ShouldContain(actual);
    }
}

[Collection("Integration Tests")]
public class ComprehensiveAutomatonTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task CreateAndExecute_ValidDFA_ShouldWork()
    {
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
        };

        var createResponse = await PostAutomatonForm(client, "/Automaton/CreateAutomaton", dfaModel);
        createResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);

        var testCases = new[]
        {
            ("a", true),     // Ends in accepting state
            ("ab", false),   // Ends in non-accepting state
            ("aba", true),   // Ends in accepting state
            ("", false)      // Empty string, starts in non-accepting state
        };

        foreach (var (input, expectedResult) in testCases)
        {
            dfaModel.Input = input;
            var response = await PostAutomatonForm(client, "/Automaton/ExecuteAll", dfaModel);
            response.StatusCode.ShouldBe(HttpStatusCode.OK);
            
            var content = await response.Content.ReadAsStringAsync();
            if (expectedResult)
            {
                content.ShouldContain("Accepted");
            }
            else
            {
                content.ShouldContain("Rejected");
            }
        }
    }

    [Fact]
    public async Task CreateAndExecute_ValidNFA_ShouldWork()
    {
        var client = GetHttpClient();

        var nfaModel = new AutomatonViewModel
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
        };

        var createResponse = await PostAutomatonForm(client, "/Automaton/CreateAutomaton", nfaModel);
        createResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);

        nfaModel.Input = "a";
        var response = await PostAutomatonForm(client, "/Automaton/ExecuteAll", nfaModel);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("Accepted");
    }

    [Fact]
    public async Task CreateAndExecute_ValidEpsilonNFA_ShouldWork()
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
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0' } // Epsilon transition
            ],
        };

        var createResponse = await PostAutomatonForm(client, "/Automaton/CreateAutomaton", epsilonModel);
        createResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);

        epsilonModel.Input = "";
        var response = await PostAutomatonForm(client, "/Automaton/ExecuteAll", epsilonModel);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("Accepted");
    }

    [Fact]
    public async Task StepByStepExecution_ShouldMaintainState()
    {
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
            Input = "ab"
        };

        var step1 = await PostAutomatonForm(client, "/Automaton/StepForward", dfaModel);
        step1.StatusCode.ShouldBe(HttpStatusCode.OK);

        var step2 = await PostAutomatonForm(client, "/Automaton/StepForward", dfaModel);
        step2.StatusCode.ShouldBe(HttpStatusCode.OK);

        var stepBack = await PostAutomatonForm(client, "/Automaton/StepBackward", dfaModel);
        stepBack.StatusCode.ShouldBe(HttpStatusCode.OK);

        var reset = await PostAutomatonForm(client, "/Automaton/BackToStart", dfaModel);
        reset.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ConvertOperations_ShouldWork()
    {
        var client = GetHttpClient();

        var nfaModel = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
            ],
        };

        var convertResponse = await PostAutomatonForm(client, "/Automaton/ConvertToDFA", nfaModel);
        convertResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
    }

    private static async Task<HttpResponseMessage> PostAutomatonForm(HttpClient client, string url, AutomatonViewModel model)
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

        for (int i = 0; i < model.States.Count; i++)
        {
            var state = model.States[i];
            formData.Add(new($"States[{i}].Id", state.Id.ToString()));
            formData.Add(new($"States[{i}].IsStart", state.IsStart.ToString().ToLower()));
            formData.Add(new($"States[{i}].IsAccepting", state.IsAccepting.ToString().ToLower()));
        }

        for (int i = 0; i < model.Transitions.Count; i++)
        {
            var transition = model.Transitions[i];
            formData.Add(new($"Transitions[{i}].FromStateId", transition.FromStateId.ToString()));
            formData.Add(new($"Transitions[{i}].ToStateId", transition.ToStateId.ToString()));
            formData.Add(new($"Transitions[{i}].Symbol", transition.Symbol == '\0' ? "?" : transition.Symbol.ToString()));
        }

        for (int i = 0; i < model.Alphabet.Count; i++)
        {
            formData.Add(new($"Alphabet[{i}]", model.Alphabet[i].ToString()));
        }

        var formContent = new FormUrlEncodedContent(formData);
        return await client.PostAsync(url, formContent);
    }
}
