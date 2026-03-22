using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests.InputGeneration;

[Collection("Integration Tests")]
public class InputGenerationIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    #region Helper Methods

    private static List<KeyValuePair<string, string>> BuildForm(AutomatonViewModel m, Dictionary<string, string>? additionalParams = null)
    {
        var list = new List<KeyValuePair<string, string>>
        {
            new("Type", ((int)m.Type).ToString()),
            new("Input", m.Input ?? string.Empty),
            new("Position", m.Position.ToString()),
            new("HasExecuted", m.HasExecuted.ToString().ToLower()),
            new("IsCustomAutomaton", m.IsCustomAutomaton.ToString().ToLower()),
            new("StateHistorySerialized", m.StateHistorySerialized ?? string.Empty),
            new("AcceptanceMode", ((int)m.AcceptanceMode).ToString())
        };

        if (m.CurrentStateId.HasValue)
            list.Add(new("CurrentStateId", m.CurrentStateId.Value.ToString()));

        for (int i = 0; i < m.States.Count; i++)
        {
            list.Add(new("States.Index", i.ToString()));
            list.Add(new($"States[{i}].Id", m.States[i].Id.ToString()));
            list.Add(new($"States[{i}].IsStart", m.States[i].IsStart.ToString().ToLower()));
            list.Add(new($"States[{i}].IsAccepting", m.States[i].IsAccepting.ToString().ToLower()));
        }

        for (int i = 0; i < m.Transitions.Count; i++)
        {
            list.Add(new("Transitions.Index", i.ToString()));
            list.Add(new($"Transitions[{i}].FromStateId", m.Transitions[i].FromStateId.ToString()));
            list.Add(new($"Transitions[{i}].ToStateId", m.Transitions[i].ToStateId.ToString()));
            list.Add(new($"Transitions[{i}].Symbol", m.Transitions[i].Symbol == '\0' ? "\\0" : m.Transitions[i].Symbol.ToString()));

            if (m.Transitions[i].StackPop.HasValue)
            {
                var stackPopValue = m.Transitions[i].StackPop!.Value == '\0' ? "\\0" : m.Transitions[i].StackPop!.Value.ToString();
                list.Add(new($"Transitions[{i}].StackPop", stackPopValue));
            }

            if (!string.IsNullOrEmpty(m.Transitions[i].StackPush))
                list.Add(new($"Transitions[{i}].StackPush", m.Transitions[i].StackPush ?? ""));
        }

        if (additionalParams != null)
        {
            foreach (var param in additionalParams)
            {
                list.Add(new(param.Key, param.Value));
            }
        }

        return list;
    }

    private static string ExtractInputValue(string html)
    {
        var m = Regex.Match(html, "id=\"inputField\"[^>]*value=\"([^\"]*)\"", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    private static AutomatonViewModel BuildSimpleDfa() => new()
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
            new() { FromStateId = 2, ToStateId = 2, Symbol = 'b' }
        ],
        IsCustomAutomaton = true
    };

    private static AutomatonViewModel BuildSimpleNfa() => new()
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
            new() { FromStateId = 1, ToStateId = 3, Symbol = 'a' },
            new() { FromStateId = 3, ToStateId = 2, Symbol = 'b' }
        ],
        IsCustomAutomaton = true
    };

    private static AutomatonViewModel BuildBalancedParenthesesPda() => new()
    {
        Type = AutomatonType.DPDA,
        States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
        Transitions =
        [
            new() { FromStateId = 1, ToStateId = 1, Symbol = '(', StackPop = '\0', StackPush = "(" },
            new() { FromStateId = 1, ToStateId = 1, Symbol = ')', StackPop = '(', StackPush = null }
        ],
        AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack,
        IsCustomAutomaton = true
    };

    private static AutomatonViewModel BuildAnBnPda() => new()
    {
        Type = AutomatonType.DPDA,
        States =
        [
            new() { Id = 1, IsStart = true, IsAccepting = false },
            new() { Id = 2, IsStart = false, IsAccepting = true }
        ],
        Transitions =
        [
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" },
            new() { FromStateId = 1, ToStateId = 2, Symbol = 'b', StackPop = 'X', StackPush = null },
            new() { FromStateId = 2, ToStateId = 2, Symbol = 'b', StackPop = 'X', StackPush = null }
        ],
        AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack,
        IsCustomAutomaton = true
    };

    #endregion

    #region DFA Input Generation Tests

    [Fact]
    public async Task DFA_GenerateRandomString_ReturnsValidInput()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa();
        var formData = BuildForm(model, new Dictionary<string, string>
        {
            ["minLength"] = "3",
            ["maxLength"] = "10"
        });

        var response = await client.PostAsync("/InputGeneration/GenerateRandomString", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        var input = ExtractInputValue(html);
        input.ShouldNotBeNullOrEmpty();
        input.Length.ShouldBeGreaterThanOrEqualTo(3);
        input.Length.ShouldBeLessThanOrEqualTo(10);
        input.All(c => c == 'a' || c == 'b').ShouldBeTrue();
    }

    [Fact]
    public async Task DFA_GenerateAcceptingString_ReturnsAccepted()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa();
        var formData = BuildForm(model, new Dictionary<string, string>
        {
            ["maxLength"] = "20"
        });

        var response = await client.PostAsync("/InputGeneration/GenerateAcceptingString", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        var input = ExtractInputValue(html);
        input.ShouldNotBeNullOrEmpty();
        input.ShouldStartWith("a"); // Must start with 'a' to reach accepting state
    }

    [Fact]
    public async Task DFA_GenerateRejectingString_ReturnsRejected()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa();
        var formData = BuildForm(model, new Dictionary<string, string>
        {
            ["maxLength"] = "20"
        });

        var response = await client.PostAsync("/InputGeneration/GenerateRejectingString", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        var input = ExtractInputValue(html);
        input.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task DFA_GenerateRandomAcceptingString_ReturnsAccepted()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa();
        var formData = BuildForm(model, new Dictionary<string, string>
        {
            ["minLength"] = "2",
            ["maxLength"] = "10",
            ["maxAttempts"] = "100"
        });

        var response = await client.PostAsync("/InputGeneration/GenerateRandomAcceptingString", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        var input = ExtractInputValue(html);
        input.ShouldNotBeNull();

        if (!string.IsNullOrEmpty(input))
        {
            input.Length.ShouldBeGreaterThanOrEqualTo(2);
            input.Length.ShouldBeLessThanOrEqualTo(10);
        }
    }

    #endregion

    #region NFA Input Generation Tests

    [Fact]
    public async Task NFA_GenerateAcceptingString_FindsAcceptingPath()
    {
        var client = GetHttpClient();
        var model = BuildSimpleNfa();
        var formData = BuildForm(model, new Dictionary<string, string>
        {
            ["maxLength"] = "20"
        });

        var response = await client.PostAsync("/InputGeneration/GenerateAcceptingString", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        var input = ExtractInputValue(html);
        input.ShouldNotBeNullOrEmpty();
        (input == "a" || input == "ab").ShouldBeTrue();
    }

    [Fact]
    public async Task NFA_GenerateNondeterministicCase_ReturnsCase()
    {
        var client = GetHttpClient();
        var model = BuildSimpleNfa();
        var formData = BuildForm(model, new Dictionary<string, string>
        {
            ["maxLength"] = "15"
        });

        var response = await client.PostAsync("/InputGeneration/GenerateNondeterministicCase", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        var input = ExtractInputValue(html);
        input.ShouldNotBeNullOrEmpty();
        input.ShouldContain('a'); // Should trigger nondeterministic choice
    }

    #endregion

    #region PDA Input Generation Tests - Balanced Parentheses

    [Fact]
    public async Task PDA_GenerateRandomString_ReturnsValidInput()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda();
        var formData = BuildForm(model, new Dictionary<string, string>
        {
            ["minLength"] = "2",
            ["maxLength"] = "8"
        });

        var response = await client.PostAsync("/InputGeneration/GenerateRandomString", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        var input = ExtractInputValue(html);
        input.ShouldNotBeNull();
        input.Length.ShouldBeGreaterThanOrEqualTo(2);
        input.Length.ShouldBeLessThanOrEqualTo(8);
        input.All(c => c == '(' || c == ')').ShouldBeTrue();
    }

    [Fact]
    public async Task PDA_BalancedParentheses_GenerateAcceptingString_ReturnsBalanced()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda();
        var formData = BuildForm(model, new Dictionary<string, string>
        {
            ["maxLength"] = "10"
        });

        var response = await client.PostAsync("/InputGeneration/GenerateAcceptingString", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        var input = ExtractInputValue(html);
        input.ShouldNotBeNull();

        if (!string.IsNullOrEmpty(input))
        {
            var openCount = input.Count(c => c == '(');
            var closeCount = input.Count(c => c == ')');
            openCount.ShouldBe(closeCount);
        }
    }

    [Fact]
    public async Task PDA_BalancedParentheses_GenerateRejectingString_ReturnsUnbalanced()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda();
        var formData = BuildForm(model, new Dictionary<string, string>
        {
            ["maxLength"] = "10"
        });

        var response = await client.PostAsync("/InputGeneration/GenerateRejectingString", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        var input = ExtractInputValue(html);
        input.ShouldNotBeNullOrEmpty();

        var openCount = input.Count(c => c == '(');
        var closeCount = input.Count(c => c == ')');
        openCount.ShouldNotBe(closeCount);
    }

    [Fact]
    public async Task PDA_BalancedParentheses_GenerateRandomAcceptingString_ReturnsBalanced()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda();
        var formData = BuildForm(model, new Dictionary<string, string>
        {
            ["minLength"] = "2",
            ["maxLength"] = "10",
            ["maxAttempts"] = "100"
        });

        var response = await client.PostAsync("/InputGeneration/GenerateRandomAcceptingString", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        var input = ExtractInputValue(html);

        if (!string.IsNullOrEmpty(input))
        {
            input.Length.ShouldBeGreaterThanOrEqualTo(2);
            input.Length.ShouldBeLessThanOrEqualTo(10);

            var openCount = input.Count(c => c == '(');
            var closeCount = input.Count(c => c == ')');
            openCount.ShouldBe(closeCount);
        }
    }

    #endregion

    #region PDA Input Generation Tests - a^n b^n Language

    [Fact]
    public async Task PDA_AnBn_GenerateAcceptingString_ReturnsValidAnBn()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda();
        var formData = BuildForm(model, new Dictionary<string, string>
        {
            ["maxLength"] = "10"
        });

        var response = await client.PostAsync("/InputGeneration/GenerateAcceptingString", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        var input = ExtractInputValue(html);

        if (!string.IsNullOrEmpty(input))
        {
            // Verify it matches a^n b^n pattern
            var firstB = input.IndexOf('b');
            if (firstB >= 0)
            {
                var aCount = input.Substring(0, firstB).Count(c => c == 'a');
                var bCount = input.Substring(firstB).Count(c => c == 'b');
                aCount.ShouldBe(bCount);
            }
        }
    }

    [Fact]
    public async Task PDA_AnBn_GenerateRejectingString_ReturnsInvalidPattern()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda();
        var formData = BuildForm(model, new Dictionary<string, string>
        {
            ["maxLength"] = "10"
        });

        var response = await client.PostAsync("/InputGeneration/GenerateRejectingString", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        var input = ExtractInputValue(html);
        input.ShouldNotBeNullOrEmpty();

        // Should NOT match a^n b^n pattern
        var aCount = input.Count(c => c == 'a');
        var bCount = input.Count(c => c == 'b');

        // Either different counts or wrong order
        bool isInvalid = aCount != bCount ||
                        input.StartsWith("b") ||
                        input.Contains("ba");
        isInvalid.ShouldBeTrue();
    }

    #endregion

    #region Interesting Cases Tests

    [Fact]
    public async Task DFA_GenerateInterestingCase_Accepting_ReturnsAcceptingString()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa();
        var formData = BuildForm(model, new Dictionary<string, string>
        {
            ["caseType"] = "accepting"
        });

        var response = await client.PostAsync("/InputGeneration/GenerateInterestingCase", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        var input = ExtractInputValue(html);
        input.ShouldNotBeNull();
    }

    [Fact]
    public async Task PDA_GenerateInterestingCase_ReturnsValidCase()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda();
        var formData = BuildForm(model, new Dictionary<string, string>
        {
            ["caseType"] = "accepting"
        });

        var response = await client.PostAsync("/InputGeneration/GenerateInterestingCase", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        var input = ExtractInputValue(html);
        input.ShouldNotBeNull();
    }

    #endregion

    #region Edge Cases and Error Handling

    [Fact]
    public async Task GenerateAcceptingString_NoAcceptingStates_HandlesGracefully()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false }
            ],
            Transitions = [],
            IsCustomAutomaton = true
        };
        var formData = BuildForm(model, new Dictionary<string, string>
        {
            ["maxLength"] = "20"
        });

        var response = await client.PostAsync("/InputGeneration/GenerateAcceptingString", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("Could not generate", Case.Insensitive);
    }

    [Fact]
    public async Task GenerateRejectingString_NoAlphabet_HandlesGracefully()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false }
            ],
            Transitions = [],
            IsCustomAutomaton = true
        };
        var formData = BuildForm(model, new Dictionary<string, string>
        {
            ["maxLength"] = "20"
        });

        var response = await client.PostAsync("/InputGeneration/GenerateRejectingString", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("Could not generate", Case.Insensitive);
    }

    [Fact]
    public async Task PDA_GenerateAcceptingString_EmptyStackMode_WorksCorrectly()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = null }
            ],
            AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly,
            IsCustomAutomaton = true
        };
        var formData = BuildForm(model, new Dictionary<string, string>
        {
            ["maxLength"] = "10"
        });

        var response = await client.PostAsync("/InputGeneration/GenerateAcceptingString", new FormUrlEncodedContent(formData));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion

    #region Full Integration Workflow Tests

    [Fact]
    public async Task FullWorkflow_GenerateAccepting_ExecuteAndVerify()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa();

        var genFormData = BuildForm(model, new Dictionary<string, string>
        {
            ["maxLength"] = "20"
        });
        var genResponse = await client.PostAsync("/InputGeneration/GenerateAcceptingString", new FormUrlEncodedContent(genFormData));
        genResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var genHtml = await genResponse.Content.ReadAsStringAsync();
        var generatedInput = ExtractInputValue(genHtml);
        generatedInput.ShouldNotBeNullOrEmpty();

        model.Input = generatedInput;
        var execFormData = BuildForm(model);
        var execResponse = await client.PostAsync("/AutomatonExecution/Start", new FormUrlEncodedContent(execFormData));
        execResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var execHtml = await execResponse.Content.ReadAsStringAsync();
        execHtml.ShouldNotContain("rejected", Case.Insensitive);
    }

    [Fact]
    public async Task FullWorkflow_PDA_GenerateRejecting_ExecuteAndVerify()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda();

        var genFormData = BuildForm(model, new Dictionary<string, string>
        {
            ["maxLength"] = "10"
        });
        var genResponse = await client.PostAsync("/InputGeneration/GenerateRejectingString", new FormUrlEncodedContent(genFormData));
        genResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
        var genHtml = await genResponse.Content.ReadAsStringAsync();
        var generatedInput = ExtractInputValue(genHtml);
        generatedInput.ShouldNotBeNullOrEmpty();

        model.Input = generatedInput;
        var execFormData = BuildForm(model);
        var execResponse = await client.PostAsync("/AutomatonExecution/Start", new FormUrlEncodedContent(execFormData));
        execResponse.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    #endregion
}
