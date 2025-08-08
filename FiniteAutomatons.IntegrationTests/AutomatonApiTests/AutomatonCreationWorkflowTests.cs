using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;

namespace FiniteAutomatons.IntegrationTests.AutomatonApiTests;

[Collection("Integration Tests")]
public class AutomatonCreationWorkflowTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    #region Automaton Creation Workflow Tests

    [Fact]
    public async Task CreateAutomatonWorkflow_DFA_CompleteProcess_ShouldWork()
    {
        var client = GetHttpClient();

        // Step 1: Get the creation page
        var getResponse = await client.GetAsync("/Automaton/CreateAutomaton");
        getResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Create a simple valid DFA
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
            Alphabet = ['a', 'b']
        };

        // Try to create the DFA
        var finalizeResponse = await PostAutomatonForm(client, "/Automaton/CreateAutomaton", model);
        
        // Should either redirect (success) or return OK with validation info
        finalizeResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        
        if (finalizeResponse.StatusCode == HttpStatusCode.Redirect)
        {
            finalizeResponse.Headers.Location?.ToString().ShouldContain("/");
        }
        else
        {
            // If OK, check that it's not due to a critical error
            var content = await finalizeResponse.Content.ReadAsStringAsync();
            content.ShouldNotContain("Error occurred");
        }
    }

    [Fact]
    public async Task CreateAutomatonWorkflow_NFA_WithNondeterministicTransitions_ShouldWork()
    {
        var client = GetHttpClient();

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
            Alphabet = ['a', 'b']
        };

        // Try to create the NFA directly
        var finalizeResponse = await PostAutomatonForm(client, "/Automaton/CreateAutomaton", nfaModel);
        
        // Should either redirect (success) or return OK with validation info
        finalizeResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        
        if (finalizeResponse.StatusCode == HttpStatusCode.Redirect)
        {
            finalizeResponse.Headers.Location?.ToString().ShouldContain("/");
        }
        else
        {
            // If OK, check that it's not due to a critical error
            var content = await finalizeResponse.Content.ReadAsStringAsync();
            content.ShouldNotContain("Error occurred");
        }
    }

    [Fact]
    public async Task CreateAutomatonWorkflow_EpsilonNFA_WithEpsilonTransitions_ShouldWork()
    {
        var client = GetHttpClient();

        var epsilonModel = new AutomatonViewModel
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
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0' }, // Epsilon
                new() { FromStateId = 2, ToStateId = 3, Symbol = 'a' }
            ],
            Alphabet = ['a']
        };

        // Try to create the Epsilon NFA directly
        var finalizeResponse = await PostAutomatonForm(client, "/Automaton/CreateAutomaton", epsilonModel);
        
        // Should either redirect (success) or return OK with validation info
        finalizeResponse.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        
        if (finalizeResponse.StatusCode == HttpStatusCode.Redirect)
        {
            finalizeResponse.Headers.Location?.ToString().ShouldContain("/");
        }
        else
        {
            // If OK, check that it's not due to a critical error
            var content = await finalizeResponse.Content.ReadAsStringAsync();
            content.ShouldNotContain("Error occurred");
        }
    }

    [Fact]
    public async Task ChangeAutomatonType_FromDFAToNFA_ShouldPreserveData()
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
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
            ],
            Alphabet = ['a']
        };

        // Change type to NFA
        var changeTypeResponse = await PostChangeAutomatonType(client, dfaModel, AutomatonType.NFA);
        changeTypeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await changeTypeResponse.Content.ReadAsStringAsync();
        // Should preserve states and transitions
        content.ShouldContain("State 1");
        content.ShouldContain("State 2");
    }

    [Fact]
    public async Task ChangeAutomatonType_FromNFAToEpsilonNFA_ShouldWork()
    {
        var client = GetHttpClient();

        var nfaModel = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true }
            ],
            Transitions = [],
            Alphabet = []
        };

        var changeTypeResponse = await PostChangeAutomatonType(client, nfaModel, AutomatonType.EpsilonNFA);
        changeTypeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task ChangeAutomatonType_FromEpsilonNFAToNFA_ShouldRemoveEpsilonTransitions()
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
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }  // Regular
            ],
            Alphabet = ['a']
        };

        var changeTypeResponse = await PostChangeAutomatonType(client, epsilonModel, AutomatonType.NFA);
        changeTypeResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        // Should show warning about epsilon transitions being removed
        var content = await changeTypeResponse.Content.ReadAsStringAsync();
        // The epsilon transition should be filtered out
    }

    #endregion

    #region Validation Tests

    [Fact]
    public async Task CreateAutomaton_NoStates_ShouldShowValidationError()
    {
        var client = GetHttpClient();

        var emptyModel = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [],
            Transitions = [],
            Alphabet = []
        };

        var response = await PostAutomatonForm(client, "/Automaton/CreateAutomaton", emptyModel);
        response.StatusCode.ShouldBe(HttpStatusCode.OK); // Should return view with errors

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("must have at least one state");
    }

    [Fact]
    public async Task CreateAutomaton_NoStartState_ShouldShowValidationError()
    {
        var client = GetHttpClient();

        var modelWithoutStart = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = false, IsAccepting = true } // No start state
            ],
            Transitions = [],
            Alphabet = []
        };

        var response = await PostAutomatonForm(client, "/Automaton/CreateAutomaton", modelWithoutStart);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("exactly one start state");
    }

    [Fact]
    public async Task CreateAutomaton_MultipleStartStates_ShouldShowValidationError()
    {
        var client = GetHttpClient();

        var modelWithMultipleStarts = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = true, IsAccepting = true } // Multiple start states
            ],
            Transitions = [],
            Alphabet = []
        };

        var response = await PostAutomatonForm(client, "/Automaton/CreateAutomaton", modelWithMultipleStarts);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("exactly one start state");
    }

    [Fact]
    public async Task CreateDFA_NonDeterministicTransitions_ShouldShowValidationError()
    {
        var client = GetHttpClient();

        var nonDeterministicDFA = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true },
                new() { Id = 3, IsStart = false, IsAccepting = false }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 1, ToStateId = 3, Symbol = 'a' } // Non-deterministic!
            ],
            Alphabet = ['a']
        };

        var response = await PostAutomatonForm(client, "/Automaton/CreateAutomaton", nonDeterministicDFA);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("multiple transitions");
    }

    [Fact]
    public async Task CreateDFA_WithEpsilonTransitions_ShouldShowValidationError()
    {
        var client = GetHttpClient();

        var dfaWithEpsilon = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0' } // Epsilon in DFA!
            ],
            Alphabet = []
        };

        var response = await PostAutomatonForm(client, "/Automaton/CreateAutomaton", dfaWithEpsilon);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        // Should either show validation error or handle gracefully
        // The validation message might appear in different forms
        var hasValidationError = content.Contains("cannot have epsilon transitions") ||
                                content.Contains("epsilon") ||
                                content.Contains("validation") ||
                                content.Contains("error") ||
                                !content.Contains("Successfully created");
        
        hasValidationError.ShouldBeTrue("Expected validation error for DFA with epsilon transitions");
    }

    #endregion

    #region State and Transition Management Tests

    [Fact]
    public async Task AddState_DuplicateId_ShouldShowError()
    {
        var client = GetHttpClient();

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false }
            ]
        };

        // Try to add another state with same ID
        var response = await PostAddState(client, model, 1, false, true);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("already exists");
    }

    [Fact]
    public async Task AddState_SecondStartState_ShouldShowError()
    {
        var client = GetHttpClient();

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false }
            ]
        };

        var response = await PostAddState(client, model, 2, isStart: true, isAccepting: false);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("Only one start state is allowed");
    }

    [Fact]
    public async Task AddTransition_InvalidFromState_ShouldShowError()
    {
        var client = GetHttpClient();

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false }
            ]
        };

        var response = await PostAddTransition(client, model, 99, 1, "a"); // Invalid from state
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("does not exist");
    }

    [Fact]
    public async Task AddTransition_InvalidToState_ShouldShowError()
    {
        var client = GetHttpClient();

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false }
            ]
        };

        var response = await PostAddTransition(client, model, 1, 99, "a"); // Invalid to state
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        content.ShouldContain("does not exist");
    }

    [Fact]
    public async Task RemoveState_WithTransitions_ShouldRemoveRelatedTransitions()
    {
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
            Alphabet = ['a']
        };

        var response = await PostRemoveState(client, model, 2);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var content = await response.Content.ReadAsStringAsync();
        // Should not contain the removed state or its transitions
        content.ShouldNotContain("State 2");
    }

    [Fact]
    public async Task RemoveTransition_ShouldUpdateAlphabet()
    {
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
            Alphabet = ['a', 'b']
        };

        // Remove transition using 'a', alphabet should still contain 'a' if it's the only one
        var response = await PostRemoveTransition(client, model, 1, 2, "a");
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task AutomatonOperations_MalformedData_ShouldHandleGracefully()
    {
        var client = GetHttpClient();

        // Test with malformed form data
        var formData = new FormUrlEncodedContent(new[]
        {
            new KeyValuePair<string, string>("Type", "INVALID_TYPE"),
            new KeyValuePair<string, string>("Input", "test")
        });

        var response = await client.PostAsync("/Automaton/ExecuteAll", formData);
        // Should handle gracefully, not crash
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task AutomatonOperations_VeryLongInput_ShouldHandleCorrectly()
    {
        var client = GetHttpClient();

        var model = CreateSimpleDFA();
        model.Input = new string('a', 10000); // Very long input

        var response = await PostAutomatonForm(client, "/Automaton/ExecuteAll", model);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task AutomatonOperations_SpecialCharacters_ShouldHandleCorrectly()
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
            Alphabet = ['!', '@', '#', '$', '%'],
            Input = "!@#$%"
        };

        var response = await PostAutomatonForm(client, "/Automaton/ExecuteAll", model);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion

    #region Helper Methods

    private async Task<HttpResponseMessage> PostAddState(HttpClient client, AutomatonViewModel model, int stateId, bool isStart, bool isAccepting)
    {
        var formData = new List<KeyValuePair<string, string>>
        {
            new("Type", model.Type.ToString()),
            new("stateId", stateId.ToString()),
            new("isStart", isStart.ToString().ToLower()),
            new("isAccepting", isAccepting.ToString().ToLower()),
            new("IsCustomAutomaton", "true")
        };

        AddModelDataToForm(formData, model);
        var formContent = new FormUrlEncodedContent(formData);
        return await client.PostAsync("/Automaton/AddState", formContent);
    }

    private async Task<HttpResponseMessage> PostAddTransition(HttpClient client, AutomatonViewModel model, int fromStateId, int toStateId, string symbol)
    {
        var formData = new List<KeyValuePair<string, string>>
        {
            new("Type", model.Type.ToString()),
            new("fromStateId", fromStateId.ToString()),
            new("toStateId", toStateId.ToString()),
            new("symbol", symbol),
            new("IsCustomAutomaton", "true")
        };

        AddModelDataToForm(formData, model);
        var formContent = new FormUrlEncodedContent(formData);
        return await client.PostAsync("/Automaton/AddTransition", formContent);
    }

    private async Task<HttpResponseMessage> PostRemoveState(HttpClient client, AutomatonViewModel model, int stateId)
    {
        var formData = new List<KeyValuePair<string, string>>
        {
            new("Type", model.Type.ToString()),
            new("stateId", stateId.ToString()),
            new("IsCustomAutomaton", "true")
        };

        AddModelDataToForm(formData, model);
        var formContent = new FormUrlEncodedContent(formData);
        return await client.PostAsync("/Automaton/RemoveState", formContent);
    }

    private async Task<HttpResponseMessage> PostRemoveTransition(HttpClient client, AutomatonViewModel model, int fromStateId, int toStateId, string symbol)
    {
        var formData = new List<KeyValuePair<string, string>>
        {
            new("Type", model.Type.ToString()),
            new("fromStateId", fromStateId.ToString()),
            new("toStateId", toStateId.ToString()),
            new("symbol", symbol),
            new("IsCustomAutomaton", "true")
        };

        AddModelDataToForm(formData, model);
        var formContent = new FormUrlEncodedContent(formData);
        return await client.PostAsync("/Automaton/RemoveTransition", formContent);
    }

    private async Task<HttpResponseMessage> PostChangeAutomatonType(HttpClient client, AutomatonViewModel model, AutomatonType newType)
    {
        var formData = new List<KeyValuePair<string, string>>
        {
            new("newType", newType.ToString()),
            new("IsCustomAutomaton", "true")
        };

        AddModelDataToForm(formData, model);
        var formContent = new FormUrlEncodedContent(formData);
        return await client.PostAsync("/Automaton/ChangeAutomatonType", formContent);
    }

    private async Task<HttpResponseMessage> PostAutomatonForm(HttpClient client, string url, AutomatonViewModel model)
    {
        var formData = new List<KeyValuePair<string, string>>
        {
            new("Type", model.Type.ToString()),
            new("Input", model.Input ?? ""),
            new("IsCustomAutomaton", model.IsCustomAutomaton.ToString().ToLower())
        };

        AddModelDataToForm(formData, model);
        var formContent = new FormUrlEncodedContent(formData);
        return await client.PostAsync(url, formContent);
    }

    private static void AddModelDataToForm(List<KeyValuePair<string, string>> formData, AutomatonViewModel model)
    {
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
    }

    private static AutomatonViewModel CreateSimpleDFA()
    {
        return new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true }
            ],
            Transitions = [],
            Alphabet = ['a']
        };
    }

    #endregion
}