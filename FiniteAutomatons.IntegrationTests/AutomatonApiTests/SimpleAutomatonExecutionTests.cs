using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;

namespace FiniteAutomatons.IntegrationTests.AutomatonApiTests;

[Collection("Integration Tests")]
public class SimpleAutomatonExecutionTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task ExecuteAll_DFA_SimpleStringRecognition_ShouldWork()
    {
        // Arrange - Simple DFA that accepts strings ending with 'a'
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
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 1, Symbol = 'b' }
            ],
        };

        var client = GetHttpClient();

        var testCases = new[]
        {
            ("a", true),     // Ends with 'a'
            ("ba", true),    // Ends with 'a'
            ("bba", true),   // Ends with 'a'
            ("b", false),    // Ends with 'b'
            ("ab", false),   // Ends with 'b'
            ("", false)      // Empty string
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
    public async Task ExecuteAll_NFA_NondeterministicBehavior_ShouldWork()
    {
        // Arrange - NFA with nondeterministic transitions
        var nfaModel = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = false },
                new() { Id = 3, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' },
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }, // Nondeterministic
                new() { FromStateId = 2, ToStateId = 3, Symbol = 'b' }
            ],
        };

        var client = GetHttpClient();

        var testCases = new[]
        {
            ("ab", true),    // Can reach accepting state
            ("aab", true),   // Can reach accepting state
            ("aaab", true),  // Can reach accepting state
            ("a", false),    // Cannot reach accepting state
            ("b", false),    // Cannot reach accepting state
            ("", false)      // Empty string
        };

        foreach (var (input, expectedResult) in testCases)
        {
            nfaModel.Input = input;
            var response = await PostAutomatonForm(client, "/Automaton/ExecuteAll", nfaModel);
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
    public async Task ExecuteAll_EpsilonNFA_EpsilonTransitions_ShouldWork()
    {
        // Arrange - Simple Epsilon NFA
        var epsilonNfaModel = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = false },
                new() { Id = 3, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0' }, // Epsilon transition
                new() { FromStateId = 2, ToStateId = 3, Symbol = 'a' }
            ]
        };

        var client = GetHttpClient();

        var testCases = new[]
        {
            ("a", true),     // Through epsilon transition then 'a'
            ("", false),     // Empty string (only epsilon but no 'a')
            ("aa", false),   // Too many 'a's
            ("b", false)     // Wrong symbol
        };

        foreach (var (input, expectedResult) in testCases)
        {
            epsilonNfaModel.Input = input;
            var response = await PostAutomatonForm(client, "/Automaton/ExecuteAll", epsilonNfaModel);
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
    public async Task StepByStep_DFA_ShouldMaintainState()
    {
        // Arrange
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

        var client = GetHttpClient();

        // Step 1: Forward
        var step1Response = await PostAutomatonForm(client, "/Automaton/StepForward", dfaModel);
        step1Response.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Step 2: Forward again
        var step2Response = await PostAutomatonForm(client, "/Automaton/StepForward", dfaModel);
        step2Response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var content2 = await step2Response.Content.ReadAsStringAsync();
        content2.ShouldContain("Rejected"); // Should be back in state 1

        // Step 3: Backward
        var backResponse = await PostAutomatonForm(client, "/Automaton/StepBackward", dfaModel);
        backResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Step 4: Reset
        var resetResponse = await PostAutomatonForm(client, "/Automaton/Reset", dfaModel);
        resetResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ConvertToDFA_FromNFA_ShouldWork()
    {
        // Arrange - Simple NFA
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

        var client = GetHttpClient();

        // Act - Convert to DFA
        var response = await PostAutomatonForm(client, "/Automaton/ConvertToDFA", nfaModel);

        // Assert - Should either redirect or return OK depending on implementation
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        
        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            response.Headers.Location?.ToString().ShouldContain("/");
        }
        else
        {
            var content = await response.Content.ReadAsStringAsync();
            content.ShouldNotContain("Error occurred");
        }
    }

    [Fact]
    public async Task ConvertToDFA_FromEpsilonNFA_ShouldWork()
    {
        // Arrange - Simple Epsilon NFA
        var epsilonNfaModel = new AutomatonViewModel
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
        };

        var client = GetHttpClient();

        // Act - Convert to DFA
        var response = await PostAutomatonForm(client, "/Automaton/ConvertToDFA", epsilonNfaModel);

        // Assert - Should either redirect or return OK depending on implementation
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        
        if (response.StatusCode == HttpStatusCode.Redirect)
        {
            response.Headers.Location?.ToString().ShouldContain("/");
        }
        else
        {
            var content = await response.Content.ReadAsStringAsync();
            // Check that it's not an error response
            content.ShouldNotContain("Error occurred");
        }
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
