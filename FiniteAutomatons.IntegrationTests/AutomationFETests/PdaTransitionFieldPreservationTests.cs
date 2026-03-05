using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests.AutomationFETests;

[Collection("Integration Tests")]
public class PdaTransitionFieldPreservationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    private static AutomatonViewModel BuildBalancedParenthesesPda(string input) => new()
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
        AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
    };

    private static AutomatonViewModel BuildAnBnPda(string input) => new()
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
        AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
    };

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, AutomatonViewModel model)
    {
        var formData = BuildFormData(model);
        return await client.PostAsync(url, new FormUrlEncodedContent(formData));
    }

    private static List<KeyValuePair<string, string>> BuildFormData(AutomatonViewModel m)
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

        if (m.IsAccepted.HasValue)
            list.Add(new("IsAccepted", m.IsAccepted.Value.ToString().ToLower()));

        if (!string.IsNullOrEmpty(m.StackSerialized))
            list.Add(new("StackSerialized", m.StackSerialized));

        if (!string.IsNullOrEmpty(m.InitialStackSerialized))
            list.Add(new("InitialStackSerialized", m.InitialStackSerialized));

        if (m.CurrentStates != null)
        {
            int i = 0;
            foreach (var s in m.CurrentStates)
            {
                list.Add(new("CurrentStates.Index", i.ToString()));
                list.Add(new($"CurrentStates[{i}]", s.ToString()));
                i++;
            }
        }

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

        return list;
    }

    private static void AssertTransitionFieldsPreserved(string html, AutomatonViewModel expectedModel)
    {
        for (int i = 0; i < expectedModel.Transitions.Count; i++)
        {
            var transition = expectedModel.Transitions[i];

            var fromMatch = Regex.Match(html, $@"name\s*=\s*""Transitions\[{i}\]\.FromStateId""[^>]*value\s*=\s*""{transition.FromStateId}""", RegexOptions.IgnoreCase);
            fromMatch.Success.ShouldBeTrue($"Transition {i} FromStateId should be present in HTML");

            if (transition.StackPop.HasValue)
            {
                var stackPopValue = transition.StackPop.Value == '\0' ? @"\\0" : Regex.Escape(transition.StackPop.Value.ToString());
                var stackPopMatch = Regex.Match(html, $@"name\s*=\s*""Transitions\[{i}\]\.StackPop""[^>]*value\s*=\s*""{stackPopValue}""", RegexOptions.IgnoreCase);

                if (!stackPopMatch.Success)
                {
                    var anyMatch = Regex.Match(html, $@"name\s*=\s*""Transitions\[{i}\]\.StackPop""[^>]*value\s*=\s*""([^""]*)""", RegexOptions.IgnoreCase);
                    var actualValue = anyMatch.Success ? anyMatch.Groups[1].Value : "NOT FOUND";
                    var actualBytes = anyMatch.Success ? string.Join(" ", anyMatch.Groups[1].Value.Select(c => $"{(int)c:X2}")) : "";
                    stackPopMatch.Success.ShouldBeTrue($"Transition {i} StackPop should be preserved. Expected pattern: {stackPopValue}, Actual value: '{actualValue}', Bytes: {actualBytes}");
                }
            }

            if (!string.IsNullOrEmpty(transition.StackPush))
            {
                var stackPushValue = Regex.Escape(transition.StackPush);
                var stackPushMatch = Regex.Match(html, $@"name\s*=\s*""Transitions\[{i}\]\.StackPush""[^>]*value\s*=\s*""{stackPushValue}""", RegexOptions.IgnoreCase);
                stackPushMatch.Success.ShouldBeTrue($"Transition {i} StackPush ('{transition.StackPush}') should be preserved in HTML hidden field");
            }
        }
    }

    [Fact]
    public async Task Start_BalancedParentheses_PreservesStackPopAndStackPush()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");

        var response = await PostAsync(client, "/AutomatonExecution/Start", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AssertTransitionFieldsPreserved(html, model);
    }

    [Fact]
    public async Task Start_AnBn_PreservesStackPopAndStackPush()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda("aabb");

        var response = await PostAsync(client, "/AutomatonExecution/Start", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AssertTransitionFieldsPreserved(html, model);
    }

    [Fact]
    public async Task Start_ComplexPda_PreservesAllTransitionFields()
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
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = "YZ" },
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'c', StackPop = 'Y', StackPush = null },
                new() { FromStateId = 2, ToStateId = 2, Symbol = '\0', StackPop = '\0', StackPush = null }
            ],
            Input = "abc",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var response = await PostAsync(client, "/AutomatonExecution/Start", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AssertTransitionFieldsPreserved(html, model);
    }

    [Fact]
    public async Task StepForward_AfterStart_PreservesStackPopAndStackPush()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");

        // Start first
        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        startResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        model.HasExecuted = true;
        model.CurrentStateId = 1;

        // Step forward
        var stepResp = await PostAsync(client, "/AutomatonExecution/StepForward", model);
        var stepHtml = await stepResp.Content.ReadAsStringAsync();

        stepResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        AssertTransitionFieldsPreserved(stepHtml, model);
    }

    [Fact]
    public async Task StepForward_MultipleSteps_PreservesStackPopAndStackPush()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda("aabb");

        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        startResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        model.HasExecuted = true;
        model.CurrentStateId = 1;

        // Step 1
        var step1Resp = await PostAsync(client, "/AutomatonExecution/StepForward", model);
        var step1Html = await step1Resp.Content.ReadAsStringAsync();
        step1Resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        AssertTransitionFieldsPreserved(step1Html, model);

        // Step 2
        model.Position = 1;
        var step2Resp = await PostAsync(client, "/AutomatonExecution/StepForward", model);
        var step2Html = await step2Resp.Content.ReadAsStringAsync();
        step2Resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        AssertTransitionFieldsPreserved(step2Html, model);
    }

    [Fact]
    public async Task ExecuteAll_BalancedParentheses_PreservesStackPopAndStackPush()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("(())");

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AssertTransitionFieldsPreserved(html, model);
    }

    [Fact]
    public async Task ExecuteAll_AnBn_PreservesStackPopAndStackPush()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda("aaabbb");

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AssertTransitionFieldsPreserved(html, model);
    }

    [Fact]
    public async Task BackToStart_AfterExecution_PreservesStackPopAndStackPush()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");

        // Execute first
        var execResp = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);
        execResp.StatusCode.ShouldBe(HttpStatusCode.OK);

        model.HasExecuted = true;
        model.Position = 2;
        model.CurrentStateId = 1;

        // Back to start
        var backResp = await PostAsync(client, "/AutomatonExecution/BackToStart", model);
        var backHtml = await backResp.Content.ReadAsStringAsync();

        backResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        AssertTransitionFieldsPreserved(backHtml, model);
    }

    [Fact]
    public async Task StepBackward_AfterSteps_PreservesStackPopAndStackPush()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");

        // Start and step forward
        await PostAsync(client, "/AutomatonExecution/Start", model);
        model.HasExecuted = true;
        model.CurrentStateId = 1;
        await PostAsync(client, "/AutomatonExecution/StepForward", model);

        model.Position = 1;

        // Step backward
        var backResp = await PostAsync(client, "/AutomatonExecution/StepBackward", model);
        var backHtml = await backResp.Content.ReadAsStringAsync();

        backResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        AssertTransitionFieldsPreserved(backHtml, model);
    }

    [Fact]
    public async Task Reset_AfterExecution_PreservesStackPopAndStackPush()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");

        // Execute first
        await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);
        model.HasExecuted = true;
        model.Position = 2;

        // Reset
        var resetResp = await PostAsync(client, "/AutomatonExecution/Reset", model);
        var resetHtml = await resetResp.Content.ReadAsStringAsync();

        resetResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        AssertTransitionFieldsPreserved(resetHtml, model);
    }

    [Fact]
    public async Task Start_EpsilonStackOperations_PreservesEpsilonCorrectly()
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
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0', StackPop = '\0', StackPush = null }
            ],
            Input = "",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var response = await PostAsync(client, "/AutomatonExecution/Start", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var stackPopMatch = Regex.Match(html, @"name\s*=\s*""Transitions\[0\]\.StackPop""[^>]*value\s*=\s*""\\0""", RegexOptions.IgnoreCase);
        stackPopMatch.Success.ShouldBeTrue("Epsilon stack pop should be preserved as \\0 in HTML");
    }

    [Fact]
    public async Task Start_MultiCharacterStackPush_PreservesAllCharacters()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "XYZ" }
            ],
            Input = "a",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var response = await PostAsync(client, "/AutomatonExecution/Start", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var stackPushMatch = Regex.Match(html, @"name\s*=\s*""Transitions\[0\]\.StackPush""[^>]*value\s*=\s*""XYZ""", RegexOptions.IgnoreCase);
        stackPushMatch.Success.ShouldBeTrue("Multi-character stack push 'XYZ' should be preserved");
    }

    [Fact]
    public async Task Start_NullStackPush_DoesNotIncludeHiddenField()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = 'X', StackPush = null }
            ],
            Input = "a",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var response = await PostAsync(client, "/AutomatonExecution/Start", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);

        var stackPushMatches = Regex.Matches(html, @"name\s*=\s*""Transitions\[0\]\.StackPush""", RegexOptions.IgnoreCase);

        if (stackPushMatches.Count > 0)
        {
            var valueMatch = Regex.Match(html, @"name\s*=\s*""Transitions\[0\]\.StackPush""[^>]*value\s*=\s*""""", RegexOptions.IgnoreCase);
            valueMatch.Success.ShouldBeTrue("If StackPush field exists for null value, it should be empty");
        }
    }

    [Fact]
    public async Task Start_MixedTransitions_PreservesAllCorrectly()
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
            Transitions =
            [
                // Regular push
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "A" },
                // Multi-char push
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = '\0', StackPush = "BC" },
                // Pop only (null push)
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'c', StackPop = 'A', StackPush = null },
                // Epsilon transition with epsilon pop
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0', StackPop = '\0', StackPush = null }
            ],
            Input = "abc",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var response = await PostAsync(client, "/AutomatonExecution/Start", model);
        var html = await response.Content.ReadAsStringAsync();

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        AssertTransitionFieldsPreserved(html, model);
    }
}
