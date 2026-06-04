using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests.AutomationExecution;

[Collection("Integration Tests")]
public class AutomatonExecutionPdaTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    // Helper Methods


    /// <summary>
    /// Builds a balanced-parentheses DPDA.
    /// <para>
    /// For <see cref="PDAAcceptanceMode.FinalStateOnly"/> (default): the epsilon 2→1 pops and
    /// immediately restores the '#' sentinel, so the machine can re-enter state 1 for subsequent
    /// '()' pairs. State 1 is accepting.
    /// </para>
    /// <para>
    /// For <see cref="PDAAcceptanceMode.FinalStateAndEmptyStack"/>: a 3-state design is used.
    /// The epsilon 2→3 pops '#' without restoring it (truly empty stack). State 3 is the sole
    /// accepting terminal, reached only when both conditions are met.
    /// </para>
    /// </summary>
    private static AutomatonViewModel BuildBalancedParenthesesPda(string input, PDAAcceptanceMode? acceptanceMode = null)
    {
        var mode = acceptanceMode ?? PDAAcceptanceMode.FinalStateOnly;

        if (mode == PDAAcceptanceMode.FinalStateAndEmptyStack)
        {
            // 3-state design: state 1 (start), state 2 (matching), state 3 (accepting, empty stack)
            // Epsilon 2→3 pops '#' permanently so both IsAccepting AND empty-stack are satisfied.
            return new AutomatonViewModel
            {
                Type = AutomatonType.DPDA,
                States =
                [
                    new() { Id = 1, IsStart = true,  IsAccepting = false },
                    new() { Id = 2, IsStart = false, IsAccepting = false },
                    new() { Id = 3, IsStart = false, IsAccepting = true  }
                ],
                Transitions =
                [
                    new() { FromStateId = 1, ToStateId = 2, Symbol = '(',  StackPop = '#', StackPush = "(#" },
                    new() { FromStateId = 2, ToStateId = 2, Symbol = '(',  StackPop = '(', StackPush = "((" },
                    new() { FromStateId = 2, ToStateId = 2, Symbol = ')',  StackPop = '(' },
                    // Pop '#' and move to terminal accepting state (stack empty → both conditions met)
                    new() { FromStateId = 2, ToStateId = 3, Symbol = '\0', StackPop = '#', StackPush = null }
                ],
                Input = input,
                AcceptanceMode = mode,
                IsCustomAutomaton = true
            };
        }

        // FinalStateOnly (default): epsilon 2→1 pops '#' then pushes it back, preserving the
        // sentinel so further '()' pairs can be read from state 1.
        return new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true },
                new() { Id = 2, IsStart = false, IsAccepting = false }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = '(',  StackPop = '#', StackPush = "(#" },
                new() { FromStateId = 2, ToStateId = 2, Symbol = '(',  StackPop = '(', StackPush = "((" },
                new() { FromStateId = 2, ToStateId = 2, Symbol = ')',  StackPop = '(' },
                // Restore '#' so state 1 can process further '()' pairs
                new() { FromStateId = 2, ToStateId = 1, Symbol = '\0', StackPop = '#', StackPush = "#" }
            ],
            Input = input,
            AcceptanceMode = mode,
            IsCustomAutomaton = true
        };
    }


    private static AutomatonViewModel BuildAnBnPda(string input, PDAAcceptanceMode? acceptanceMode = null) => new()
    {
        Type = AutomatonType.DPDA,
        States =
        [
            new() { Id = 1, IsStart = true, IsAccepting = true },
            new() { Id = 2, IsStart = false, IsAccepting = false },
            new() { Id = 3, IsStart = false, IsAccepting = false },
            new() { Id = 4, IsStart = false, IsAccepting = true }
        ],
        Transitions =
        [
            new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '#', StackPush = "X#" },
            new() { FromStateId = 2, ToStateId = 2, Symbol = 'a', StackPop = 'X', StackPush = "XX" },
            new() { FromStateId = 2, ToStateId = 3, Symbol = 'b', StackPop = 'X' },
            new() { FromStateId = 3, ToStateId = 3, Symbol = 'b', StackPop = 'X' },
            new() { FromStateId = 3, ToStateId = 4, Symbol = '\0', StackPop = '#' }
        ],
        Input = input,
        AcceptanceMode = acceptanceMode ?? PDAAcceptanceMode.FinalStateOnly,
        IsCustomAutomaton = true
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

    private static int ExtractPosition(string html)
    {
        var m = Regex.Match(html, "name=\"Position\"[^>]*value=\"(\\d+)\"", RegexOptions.IgnoreCase);
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    private static int? ExtractCurrentStateId(string html)
    {
        var m = Regex.Match(html, "name=\"CurrentStateId\"[^>]*value=\"(\\d+)\"", RegexOptions.IgnoreCase);
        return m.Success ? int.Parse(m.Groups[1].Value) : null;
    }

    private static bool? ExtractIsAccepted(string html)
    {
        var m = Regex.Match(html, "name=\"IsAccepted\"[^>]*value=\"(true|false)\"", RegexOptions.IgnoreCase);
        return m.Success ? bool.Parse(m.Groups[1].Value) : null;
    }

    [Fact]
    public async Task Start_BalancedParentheses_ShouldInitializeExecution()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");

        var response = await PostAsync(client, "/AutomatonExecution/Start", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractPosition(html).ShouldBe(0);
        ExtractCurrentStateId(html).ShouldBe(1);
    }

    [Fact]
    public async Task StepForward_BalancedParentheses_OpenParen_ShouldAdvancePosition()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");

        var startResponse = await PostAsync(client, "/AutomatonExecution/Start", model);
        var startHtml = await startResponse.Content.ReadAsStringAsync();

        model.HasExecuted = true;
        model.CurrentStateId = ExtractCurrentStateId(startHtml);

        var stepResponse = await PostAsync(client, "/AutomatonExecution/StepForward", model);
        stepResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var stepHtml = await stepResponse.Content.ReadAsStringAsync();
        ExtractPosition(stepHtml).ShouldBe(1);
        ExtractCurrentStateId(stepHtml).ShouldBe(2);
    }

    [Fact]
    public async Task ExecuteAll_BalancedParentheses_Valid_ShouldAccept()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("(())");

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractPosition(html).ShouldBe(4);
        ExtractIsAccepted(html).ShouldBe(true);
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task ExecuteAll_DPDA_WithConfiguredInitialStack_ShouldApplyInitialStack()
    {
        var client = GetHttpClient();
        // InitialStackSerialized is bottom-first: '#' at index 0, 'X' at index 1 (top).
        // Reading 'x' pops 'X', leaving ['#']. AcceptanceMode = FinalStateOnly so '#' on stack is fine.
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'x', StackPop = 'X', StackPush = null }],
            Input = "x",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly,
            InitialStackSerialized = JsonSerializer.Serialize(new List<char> { '#', 'X' })
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(true);
    }

    [Fact]
    public async Task ExecuteAll_NPDA_WithConfiguredInitialStack_ShouldApplyInitialStack()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = 'X', StackPush = null }],
            Input = "a",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly,
            InitialStackSerialized = JsonSerializer.Serialize(new List<char> { '#', 'X' })
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(true);
    }

    [Fact]
    public async Task ExecuteAll_BalancedParentheses_Invalid_ShouldReject()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("(()");

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(false);
        html.ShouldContain("REJECTED", Case.Insensitive);
    }

    [Fact]
    public async Task BackToStart_BalancedParentheses_ShouldResetPosition()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");

        var execResponse = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var execHtml = await execResponse.Content.ReadAsStringAsync();
        ExtractPosition(execHtml).ShouldBe(2);

        model.HasExecuted = true;
        model.Position = 2;
        model.CurrentStateId = ExtractCurrentStateId(execHtml);

        var backResponse = await PostAsync(client, "/AutomatonExecution/BackToStart", model);
        backResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var backHtml = await backResponse.Content.ReadAsStringAsync();
        ExtractPosition(backHtml).ShouldBe(0);
    }

    [Fact]
    public async Task Reset_BalancedParentheses_ShouldClearInputAndExecution()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");
        _ = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);
        model.HasExecuted = true;
        model.Position = 2;

        var resetResponse = await PostAsync(client, "/AutomatonExecution/Reset", model);
        resetResponse.StatusCode.ShouldBe(HttpStatusCode.OK);

        var resetHtml = await resetResponse.Content.ReadAsStringAsync();
        var inputMatch = Regex.Match(resetHtml, "id=\"inputField\"[^>]*value=\"([^\"]*)\"");
        inputMatch.Success.ShouldBeTrue();
        inputMatch.Groups[1].Value.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task Start_AnBn_ShouldInitializeExecution()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda("aabb");

        var response = await PostAsync(client, "/AutomatonExecution/Start", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractPosition(html).ShouldBe(0);
        ExtractCurrentStateId(html).ShouldBe(1);
    }

    [Fact]
    public async Task ExecuteAll_AnBn_Valid_ShouldAccept()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda("aaabbb");

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(true);
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task ExecuteAll_AnBn_WrongCount_ShouldReject()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda("aaabb");

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(false);
        html.ShouldContain("REJECTED", Case.Insensitive);
    }

    [Fact]
    public async Task ExecuteAll_AnBn_EmptyInput_ShouldAccept()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda("");

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(true);
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task ExecuteAll_FinalStateOnly_AcceptingStateWithNonEmptyStack_ShouldAccept()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" }
            ],
            Input = "a",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(true);
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task ExecuteAll_FinalStateOnly_NonAcceptingState_ShouldReject()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false }
            ],
            Transitions = [],
            Input = "",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(false);
        html.ShouldContain("REJECTED", Case.Insensitive);
    }

    [Fact]
    public async Task ExecuteAll_EmptyStackOnly_EmptyStackInNonAcceptingState_ShouldAccept()
    {
        var client = GetHttpClient();
        // Accepts exactly "ab":
        //   State 1 (start): 'a' on ∅ → state 2, push X
        //   State 2: 'b' on X → state 3 (no more consuming transitions)
        //   State 3: ε on # → state 4 (pops sentinel; dedicated state avoids determinism conflict)
        //   State 4 (not accepting, empty stack): EmptyStackOnly accepts
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true,  IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = false },
                new() { Id = 3, IsStart = false, IsAccepting = false },
                new() { Id = 4, IsStart = false, IsAccepting = false }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '\0', StackPush = "X" },
                new() { FromStateId = 2, ToStateId = 3, Symbol = 'b', StackPop = 'X', StackPush = null },
                // Dedicated epsilon state: no consuming transitions here, so no determinism conflict
                new() { FromStateId = 3, ToStateId = 4, Symbol = '\0', StackPop = '#', StackPush = null }
            ],
            Input = "ab",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(true);
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task ExecuteAll_EmptyStackOnly_NonEmptyStack_ShouldReject()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" }
            ],
            Input = "a",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(false);
        html.ShouldContain("REJECTED", Case.Insensitive);
    }

    [Fact]
    public async Task ExecuteAll_FinalStateAndEmptyStack_BothConditionsMet_ShouldAccept()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()", PDAAcceptanceMode.FinalStateAndEmptyStack);

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(true);
    }

    [Fact]
    public async Task ExecuteAll_FinalStateAndEmptyStack_OnlyFinalState_ShouldReject()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" }
            ],
            Input = "a",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(false);
    }

    [Fact]
    public async Task ExecuteAll_FinalStateAndEmptyStack_OnlyEmptyStack_ShouldReject()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = null }
            ],
            Input = "ab",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(false);
    }

    [Fact]
    public async Task ExecuteAll_MultiCharacterPush_ShouldWorkCorrectly()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "XY" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = null },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'c', StackPop = 'Y', StackPush = null },
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0', StackPop = '\0', StackPush = null }
            ],
            Input = "abc",
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly,
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(true);
    }

    [Fact]
    public async Task ExecuteAll_PopMismatch_ShouldReject()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = 'X', StackPush = null }
            ],
            Input = "a",
            IsCustomAutomaton = true
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(false);
    }

    [Fact]
    public async Task ExecuteAll_EpsilonToAcceptingState_ShouldAccept()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
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
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly,
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(true);
    }

    [Fact]
    public async Task ExecuteAll_InterleavedEpsilonAndConsuming_ShouldAccept()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" },
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0', StackPop = '\0', StackPush = null },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'b', StackPop = 'X', StackPush = null }
            ],
            Input = "ab",
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly,
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(true);
    }

    [Fact]
    public async Task ExecuteAll_EmptyInputAcceptingStartState_ShouldAccept()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true }
            ],
            Transitions = [],
            Input = "",
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly,
            IsCustomAutomaton = true
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(true);
    }

    [Fact]
    public async Task ExecuteAll_EmptyInputNonAcceptingStartState_ShouldReject()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false }
            ],
            Transitions = [],
            Input = "",
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly,
            IsCustomAutomaton = true
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(false);
    }

    [Fact]
    public async Task ExecuteAll_LongBalancedString_ShouldAccept()
    {
        var client = GetHttpClient();
        var longInput = string.Concat(Enumerable.Repeat("()", 50));
        var model = BuildBalancedParenthesesPda(longInput);

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(true);
    }

    [Fact]
    public async Task ExecuteAll_NestedParenthesesVeryDeep_ShouldAccept()
    {
        var client = GetHttpClient();
        var openParens = string.Concat(Enumerable.Repeat("(", 30));
        var closeParens = string.Concat(Enumerable.Repeat(")", 30));
        var model = BuildBalancedParenthesesPda(openParens + closeParens);

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(true);
    }

    [Fact]
    public async Task ExecuteAll_NPDA_FinalStateOnly_AcceptsWithNonEmptyStack()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" }],
            Input = "a",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(true);
    }

    [Fact]
    public async Task ExecuteAll_NPDA_EmptyStackOnly_AcceptsInNonAcceptingState()
    {
        var client = GetHttpClient();
        // Accepts exactly "ab" via EmptyStackOnly:
        //   State 1 (start): 'a' on ∅ → state 2, push X
        //   State 2: 'b' on X → state 3
        //   State 3: ε on # → state 4 (dedicated epsilon state to clear the sentinel)
        //   State 4: empty stack → EmptyStackOnly accepts
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States =
            [
                new() { Id = 1, IsStart = true,  IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = false },
                new() { Id = 3, IsStart = false, IsAccepting = false },
                new() { Id = 4, IsStart = false, IsAccepting = false }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '\0', StackPush = "X" },
                new() { FromStateId = 2, ToStateId = 3, Symbol = 'b', StackPop = 'X', StackPush = null },
                // Dedicated epsilon state: no consuming transitions here
                new() { FromStateId = 3, ToStateId = 4, Symbol = '\0', StackPop = '#', StackPush = null }
            ],
            Input = "ab",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(true);
    }

    [Fact]
    public async Task ExecuteAll_NPDA_FinalStateAndEmptyStack_BothConditionsMet_ShouldAccept()
    {
        var client = GetHttpClient();
        // NPDA: accepts "ab" by final state AND empty stack.
        // After reading 'b', stack = ['#']. Epsilon from state 2→3 (accepting) pops '#', making stack empty.
        // NPDA epsilon closure applies this before evaluating acceptance.
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = false },
                new() { Id = 3, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '\0', StackPush = "X" },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'b', StackPop = 'X', StackPush = null },
                // Epsilon from 2→3 pops the '#' sentinel once all X's are gone
                new() { FromStateId = 2, ToStateId = 3, Symbol = '\0', StackPop = '#', StackPush = null }
            ],
            Input = "ab",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(true);
    }

    [Fact]
    public async Task ExecuteAll_NPDA_FinalStateAndEmptyStack_OnlyFinalState_ShouldReject()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions = [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '\0', StackPush = "X" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = '\0', StackPop = '#' }
            ],
            Input = "a",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(false);
    }

    [Fact]
    public async Task ExecuteAll_NPDA_FinalStateAndEmptyStack_OnlyEmptyStack_ShouldReject()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = null },
                new() { FromStateId = 1, ToStateId = 1, Symbol = '\0', StackPop = '#' }
            ],
            Input = "ab",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(false);
    }

    [Fact]
    public async Task ExecuteAll_DPDA_WithTopFirstInitialStackSerialization_ShouldNormalizeAndAccept()
    {
        var client = GetHttpClient();
        // InitialStackSerialized ['X','#'] is in top-first order and should be normalized to bottom-first.
        // Reading 'x' pops 'X', leaving ['#']. AcceptanceMode = FinalStateOnly so '#' on stack is fine.
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'x', StackPop = 'X', StackPush = null }],
            Input = "x",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly,
            // Legacy top-first order should be normalized to bottom-first (#, X)
            InitialStackSerialized = JsonSerializer.Serialize(new List<char> { 'X', '#' })
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(true);
    }

    [Fact]
    public async Task ExecuteAll_NPDA_NoValidBranch_ShouldReject()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = 'X', StackPush = null }
            ],
            Input = "a",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        ExtractIsAccepted(html).ShouldBe(false);
    }

    [Fact]
    public async Task ExecuteAll_NondeterministicDpda_ShouldShowDeterminismError()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true },
                new() { Id = 3, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = 'X', StackPush = null },
                new() { FromStateId = 1, ToStateId = 3, Symbol = 'a', StackPop = 'X', StackPush = null }
            ],
            Input = "a",
            IsCustomAutomaton = true,
            InitialStackSerialized = JsonSerializer.Serialize(new List<char> { '#', 'X' })
        };

        var response = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("Cannot simulate as DPDA because the automaton is nondeterministic", Case.Insensitive);
    }
}

