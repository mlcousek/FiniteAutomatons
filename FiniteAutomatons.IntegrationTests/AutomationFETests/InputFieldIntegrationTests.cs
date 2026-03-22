using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests.AutomationFETests;

[Collection("Integration Tests")]
public class InputFieldIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    private static AutomatonViewModel BuildSimpleDfa(string input) => new()
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
        Input = input,
        IsCustomAutomaton = true
    };

    private static AutomatonViewModel BuildSimpleNfa(string input) => new()
    {
        Type = AutomatonType.NFA,
        States =
        [
            new() { Id = 1, IsStart = true, IsAccepting = false },
            new() { Id = 2, IsStart = false, IsAccepting = true },
            new() { Id = 3, IsStart = false, IsAccepting = true }
        ],
        Transitions =
        [
            new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
            new() { FromStateId = 1, ToStateId = 3, Symbol = 'a' }
        ],
        Input = input,
        IsCustomAutomaton = true
    };

    // Helper: post model
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, AutomatonViewModel model)
    {
        var formData = BuildForm(model);
        return await client.PostAsync(url, new FormUrlEncodedContent(formData));
    }

    private static List<KeyValuePair<string, string>> BuildForm(AutomatonViewModel m)
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

    private static string ExtractInputValue(string html)
    {
        var m = Regex.Match(html, "id=\"inputField\"[^>]*value=\"([^\"]*)\"", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    private static bool InputIsReadonly(string html)
    {
        var m = Regex.Match(html, "<input[^>]*id=\"inputField\"[^>]*", RegexOptions.IgnoreCase);
        return m.Success && m.Value.Contains("readonly", StringComparison.OrdinalIgnoreCase);
    }

    private static int ExtractPosition(string html)
    {
        var m = Regex.Match(html, "name=\"Position\"[^>]*value=\"(\\d+)\"", RegexOptions.IgnoreCase);
        return m.Success ? int.Parse(m.Groups[1].Value) : 0;
    }

    private static char? ExtractNextSymbolHighlight(string html)
    {
        var m = Regex.Match(html, @"<span[^>]*class=""symbol-highlight""[^>]*>'(?<c>.)'</span", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        return m.Success ? m.Groups["c"].Value[0] : null;
    }

    private static int CountSymbolHighlightSpans(string html)
    {
        return Regex.Matches(html, @"<span[^>]*class=""symbol-highlight""", RegexOptions.IgnoreCase | RegexOptions.Singleline).Count;
    }

    private static string ExtractStackSerialized(string html)
    {
        var m = Regex.Match(html, @"name\s*=\s*""StackSerialized""[^>]*value\s*=\s*""([^""]*)""|value\s*=\s*""([^""]*)""[^>]*name\s*=\s*""StackSerialized""", RegexOptions.IgnoreCase);
        var value = m.Success ? (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value) : string.Empty;
        return WebUtility.HtmlDecode(value);
    }

    private static string ExtractStateHistorySerialized(string html)
    {
        var m = Regex.Match(html, @"name\s*=\s*""StateHistorySerialized""[^>]*value\s*=\s*""([^""]*)""|value\s*=\s*""([^""]*)""[^>]*name\s*=\s*""StateHistorySerialized""", RegexOptions.IgnoreCase);
        var value = m.Success ? (m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value) : string.Empty;
        return WebUtility.HtmlDecode(value);
    }

    [Fact]
    public async Task InputField_BeforeStart_ShouldBeEditable()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("abba");
        var resp = await PostAsync(client, "/AutomatonCreation/CreateAutomaton", model); // creation may return OK if validation passes
        resp.StatusCode.ShouldBeOneOf([HttpStatusCode.OK, HttpStatusCode.Found]);
        var html = await resp.Content.ReadAsStringAsync();
        ExtractInputValue(html).ShouldBe("abba");
        InputIsReadonly(html).ShouldBeFalse();
    }

    [Fact]
    public async Task Start_ShouldPreserveFullInputAndMakeReadonly()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("abba");
        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        startResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await startResp.Content.ReadAsStringAsync();
        ExtractInputValue(html).ShouldBe("abba");
        InputIsReadonly(html).ShouldBeTrue();
        ExtractPosition(html).ShouldBe(0); // position after BackToStart
        ExtractNextSymbolHighlight(html).ShouldBe('a');
    }

    [Fact]
    public async Task StepForward_ShouldAdvancePositionAndKeepInput()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("abba");
        var start = await PostAsync(client, "/AutomatonExecution/Start", model);
        var startModelHtml = await start.Content.ReadAsStringAsync();
        ExtractPosition(startModelHtml).ShouldBe(0);
        var startModel = BuildSimpleDfa("abba"); // reconstruct minimal for step forward
        startModel.HasExecuted = true;
        var csMatch = Regex.Match(startModelHtml, "name=\"CurrentStateId\"[^>]*value=\"(\\d+)\"", RegexOptions.IgnoreCase);
        if (csMatch.Success) startModel.CurrentStateId = int.Parse(csMatch.Groups[1].Value);
        var stepResp = await PostAsync(client, "/AutomatonExecution/StepForward", startModel);
        stepResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stepHtml = await stepResp.Content.ReadAsStringAsync();
        ExtractInputValue(stepHtml).ShouldBe("abba");
        InputIsReadonly(stepHtml).ShouldBeTrue();
        ExtractPosition(stepHtml).ShouldBe(1);
        ExtractNextSymbolHighlight(stepHtml).ShouldBe('b');
    }

    [Fact]
    public async Task BackToStart_ShouldResetPositionButKeepInput()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("abba");
        var execHtml = await (await PostAsync(client, "/AutomatonExecution/ExecuteAll", model)).Content.ReadAsStringAsync();
        ExtractPosition(execHtml).ShouldBe(4);
        var execModel = BuildSimpleDfa("abba");
        execModel.HasExecuted = true;
        execModel.Position = 4;
        var csMatch = Regex.Match(execHtml, "name=\"CurrentStateId\"[^>]*value=\"(\\d+)\"", RegexOptions.IgnoreCase);
        if (csMatch.Success) execModel.CurrentStateId = int.Parse(csMatch.Groups[1].Value);
        var backResp = await PostAsync(client, "/AutomatonExecution/BackToStart", execModel);
        backResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var backHtml = await backResp.Content.ReadAsStringAsync();
        ExtractInputValue(backHtml).ShouldBe("abba");
        ExtractPosition(backHtml).ShouldBe(0);
        InputIsReadonly(backHtml).ShouldBeTrue();
        ExtractNextSymbolHighlight(backHtml).ShouldBe('a');
    }

    [Fact]
    public async Task ExecuteAll_ShouldReachEndAndKeepInput()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("ab");
        var resp = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await resp.Content.ReadAsStringAsync();
        ExtractInputValue(html).ShouldBe("ab");
        ExtractPosition(html).ShouldBe(2);
        InputIsReadonly(html).ShouldBeTrue();
    }

    [Fact]
    public async Task Reset_ShouldClearInputAndRemoveReadonly()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("ab");
        _ = await (await PostAsync(client, "/AutomatonExecution/ExecuteAll", model)).Content.ReadAsStringAsync();
        var execModel = BuildSimpleDfa("ab");
        execModel.HasExecuted = true;
        execModel.Position = 2;
        var resetResp = await PostAsync(client, "/AutomatonExecution/Reset", execModel);
        resetResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resetHtml = await resetResp.Content.ReadAsStringAsync();
        ExtractInputValue(resetHtml).ShouldBe(string.Empty);
        InputIsReadonly(resetHtml).ShouldBeFalse();
        ExtractPosition(resetHtml).ShouldBe(0);
    }

    [Fact]
    public async Task Start_Nfa_ShouldMakeInputReadonly()
    {
        var client = GetHttpClient();
        var model = BuildSimpleNfa("a");
        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        startResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await startResp.Content.ReadAsStringAsync();
        ExtractInputValue(html).ShouldBe("a");
        InputIsReadonly(html).ShouldBeTrue();
    }

    // ---------------- New tests for additional input strings ----------------

    [Fact]
    public async Task Start_ShouldShowFullInputAndHighlightFirstChar_For_acb()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("acb");
        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        startResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await startResp.Content.ReadAsStringAsync();
        ExtractInputValue(html).ShouldBe("acb");
        ExtractPosition(html).ShouldBe(0);
        ExtractNextSymbolHighlight(html).ShouldBe('a');
    }

    [Fact]
    public async Task Start_ShouldShowFullInputAndHighlightFirstChar_For_cadc()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'c' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'd' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'c' }
            ],
            Input = "cadc",
            IsCustomAutomaton = true
        };
        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        startResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await startResp.Content.ReadAsStringAsync();
        ExtractInputValue(html).ShouldBe("cadc");
        ExtractPosition(html).ShouldBe(0);
        ExtractNextSymbolHighlight(html).ShouldBe('c');
    }

    [Fact]
    public async Task StepForward_ShouldHighlightSecondChar_For_cadc()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'c' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'd' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'c' }
            ],
            Input = "cadc",
            IsCustomAutomaton = true
        };
        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        startResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var startHtml = await startResp.Content.ReadAsStringAsync();
        ExtractPosition(startHtml).ShouldBe(0);
        var stepModel = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = model.States,
            Transitions = model.Transitions,
            Input = model.Input,
            IsCustomAutomaton = true,
            HasExecuted = true,
            CurrentStateId = 2
        };
        var stepResp = await PostAsync(client, "/AutomatonExecution/StepForward", stepModel);
        stepResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stepHtml = await stepResp.Content.ReadAsStringAsync();
        ExtractInputValue(stepHtml).ShouldBe("cadc");
        ExtractPosition(stepHtml).ShouldBe(1);
        ExtractNextSymbolHighlight(stepHtml).ShouldBe('a');
    }

    [Fact]
    public async Task Start_ShouldShowFullInputAndOtherChars_Unhighlighted_For_acb()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("acb");
        var resp = await PostAsync(client, "/AutomatonExecution/Start", model);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await resp.Content.ReadAsStringAsync();
        var value = ExtractInputValue(html);
        value.ShouldBe("acb");
        CountSymbolHighlightSpans(html).ShouldBe(1);
        ExtractNextSymbolHighlight(html).ShouldBe('a');
        value.Contains('c').ShouldBeTrue();
        value.Contains('b').ShouldBeTrue();
    }

    [Fact]
    public async Task StepForward_ShouldShowFullInputAndOtherChars_Unhighlighted_For_acb()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("acb");
        var start = await PostAsync(client, "/AutomatonExecution/Start", model);
        start.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stepModel = BuildSimpleDfa("acb");
        stepModel.HasExecuted = true;
        stepModel.CurrentStateId = 2; // after consuming 'a'
        var stepResp = await PostAsync(client, "/AutomatonExecution/StepForward", stepModel);
        stepResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await stepResp.Content.ReadAsStringAsync();
        var value = ExtractInputValue(html);
        value.ShouldBe("acb");
        CountSymbolHighlightSpans(html).ShouldBe(0);
        ExtractNextSymbolHighlight(html).ShouldBeNull();
        value.Contains('a').ShouldBeTrue();
        value.Contains('b').ShouldBeTrue();
    }

    [Fact]
    public async Task Start_ShouldShowFullInputAndOtherChars_Unhighlighted_For_cadc()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'c' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'd' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'c' }
            ],
            Input = "cadc",
            IsCustomAutomaton = true
        };
        var resp = await PostAsync(client, "/AutomatonExecution/Start", model);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await resp.Content.ReadAsStringAsync();
        var value = ExtractInputValue(html);
        value.ShouldBe("cadc");
        CountSymbolHighlightSpans(html).ShouldBe(1);
        ExtractNextSymbolHighlight(html).ShouldBe('c');
        value.Contains('a').ShouldBeTrue();
        value.Contains('d').ShouldBeTrue();
        value.LastIndexOf('c').ShouldBeGreaterThan(0); // ensure trailing 'c' also present
    }

    [Fact]
    public async Task StepForward_ShouldShowFullInputAndOtherChars_Unhighlighted_For_cadc()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'c' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'd' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'c' }
            ],
            Input = "cadc",
            IsCustomAutomaton = true
        };
        var start = await PostAsync(client, "/AutomatonExecution/Start", model);
        start.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stepModel = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = model.States,
            Transitions = model.Transitions,
            Input = model.Input,
            IsCustomAutomaton = true,
            HasExecuted = true,
            CurrentStateId = 2
        };
        var stepResp = await PostAsync(client, "/AutomatonExecution/StepForward", stepModel);
        stepResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await stepResp.Content.ReadAsStringAsync();
        var value = ExtractInputValue(html);
        value.ShouldBe("cadc");
        CountSymbolHighlightSpans(html).ShouldBe(1);
        ExtractNextSymbolHighlight(html).ShouldBe('a');
        value.Contains('d').ShouldBeTrue();
        value.LastIndexOf('c').ShouldBeGreaterThan(0);
    }

    private static AutomatonViewModel BuildBalancedParenthesesPda(string input) => new()
    {
        Type = AutomatonType.DPDA,
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
        Type = AutomatonType.DPDA,
        States =
        [
            new() { Id = 1, IsStart = true, IsAccepting = true },
            new() { Id = 2, IsStart = false, IsAccepting = true }
        ],
        Transitions =
        [
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" },
            new() { FromStateId = 1, ToStateId = 2, Symbol = 'b', StackPop = 'X', StackPush = null },
            new() { FromStateId = 2, ToStateId = 2, Symbol = 'b', StackPop = 'X', StackPush = null }
        ],
        Input = input,
        IsCustomAutomaton = true,
        AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
    };

    [Fact]
    public async Task Pda_InputField_BeforeStart_ShouldBeEditable()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");
        var resp = await PostAsync(client, "/AutomatonCreation/CreateAutomaton", model);
        resp.StatusCode.ShouldBeOneOf([HttpStatusCode.OK, HttpStatusCode.Found]);
        var html = await resp.Content.ReadAsStringAsync();
        ExtractInputValue(html).ShouldBe("()");
        InputIsReadonly(html).ShouldBeFalse();
    }

    [Fact]
    public async Task Pda_Start_ShouldPreserveFullInputAndMakeReadonly()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("(())");
        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        startResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await startResp.Content.ReadAsStringAsync();
        ExtractInputValue(html).ShouldBe("(())");
        InputIsReadonly(html).ShouldBeTrue();
        ExtractPosition(html).ShouldBe(0);
        ExtractNextSymbolHighlight(html).ShouldBe('(');
    }

    [Fact]
    public async Task Pda_StepForward_ShouldAdvancePositionAndKeepInput()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");
        var start = await PostAsync(client, "/AutomatonExecution/Start", model);
        var startModelHtml = await start.Content.ReadAsStringAsync();
        ExtractPosition(startModelHtml).ShouldBe(0);

        var startModel = BuildBalancedParenthesesPda("()");
        startModel.HasExecuted = true;
        var csMatch = Regex.Match(startModelHtml, "name=\"CurrentStateId\"[^>]*value=\"(\\d+)\"", RegexOptions.IgnoreCase);
        if (csMatch.Success) startModel.CurrentStateId = int.Parse(csMatch.Groups[1].Value);

        var stepResp = await PostAsync(client, "/AutomatonExecution/StepForward", startModel);
        stepResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stepHtml = await stepResp.Content.ReadAsStringAsync();
        ExtractInputValue(stepHtml).ShouldBe("()");
        InputIsReadonly(stepHtml).ShouldBeTrue();
        ExtractPosition(stepHtml).ShouldBe(1);
        ExtractNextSymbolHighlight(stepHtml).ShouldBe(')');
    }

    [Fact]
    public async Task Pda_BackToStart_ShouldResetPositionButKeepInput()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");
        var execHtml = await (await PostAsync(client, "/AutomatonExecution/ExecuteAll", model)).Content.ReadAsStringAsync();
        ExtractPosition(execHtml).ShouldBe(2);

        var execModel = BuildBalancedParenthesesPda("()");
        execModel.HasExecuted = true;
        execModel.Position = 2;
        var csMatch = Regex.Match(execHtml, "name=\"CurrentStateId\"[^>]*value=\"(\\d+)\"", RegexOptions.IgnoreCase);
        if (csMatch.Success) execModel.CurrentStateId = int.Parse(csMatch.Groups[1].Value);

        var backResp = await PostAsync(client, "/AutomatonExecution/BackToStart", execModel);
        backResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var backHtml = await backResp.Content.ReadAsStringAsync();
        ExtractInputValue(backHtml).ShouldBe("()");
        ExtractPosition(backHtml).ShouldBe(0);
        InputIsReadonly(backHtml).ShouldBeTrue();
        ExtractNextSymbolHighlight(backHtml).ShouldBe('(');
    }

    [Fact]
    public async Task Pda_ExecuteAll_ShouldReachEndAndKeepInput()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("(())");
        var resp = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await resp.Content.ReadAsStringAsync();
        ExtractInputValue(html).ShouldBe("(())");
        ExtractPosition(html).ShouldBe(4);
        InputIsReadonly(html).ShouldBeTrue();
    }

    [Fact]
    public async Task Pda_Reset_ShouldClearInputAndRemoveReadonly()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");
        _ = await (await PostAsync(client, "/AutomatonExecution/ExecuteAll", model)).Content.ReadAsStringAsync();

        var execModel = BuildBalancedParenthesesPda("()");
        execModel.HasExecuted = true;
        execModel.Position = 2;

        var resetResp = await PostAsync(client, "/AutomatonExecution/Reset", execModel);
        resetResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var resetHtml = await resetResp.Content.ReadAsStringAsync();
        ExtractInputValue(resetHtml).ShouldBe(string.Empty);
        InputIsReadonly(resetHtml).ShouldBeFalse();
        ExtractPosition(resetHtml).ShouldBe(0);
    }

    [Fact]
    public async Task Pda_AnBn_Start_ShouldShowFullInputAndHighlightFirstChar()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda("aabb");
        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        startResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await startResp.Content.ReadAsStringAsync();
        ExtractInputValue(html).ShouldBe("aabb");
        ExtractPosition(html).ShouldBe(0);
        ExtractNextSymbolHighlight(html).ShouldBe('a');
    }

    [Fact]
    public async Task Pda_AnBn_StepForward_ShouldHighlightSecondChar()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda("aabb");
        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        startResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var startHtml = await startResp.Content.ReadAsStringAsync();
        ExtractPosition(startHtml).ShouldBe(0);

        var stepModel = BuildAnBnPda("aabb");
        stepModel.HasExecuted = true;
        stepModel.CurrentStateId = 1;

        var stepResp = await PostAsync(client, "/AutomatonExecution/StepForward", stepModel);
        stepResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stepHtml = await stepResp.Content.ReadAsStringAsync();
        ExtractInputValue(stepHtml).ShouldBe("aabb");
        ExtractPosition(stepHtml).ShouldBe(1);
        ExtractNextSymbolHighlight(stepHtml).ShouldBe('a');
    }

    [Fact]
    public async Task Pda_LongBalancedParentheses_PreservesInputThroughExecution()
    {
        var client = GetHttpClient();
        var longInput = string.Concat(Enumerable.Repeat("()", 10));
        var model = BuildBalancedParenthesesPda(longInput);

        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        var startHtml = await startResp.Content.ReadAsStringAsync();
        ExtractInputValue(startHtml).ShouldBe(longInput);

        var execResp = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var execHtml = await execResp.Content.ReadAsStringAsync();
        ExtractInputValue(execHtml).ShouldBe(longInput);
        ExtractPosition(execHtml).ShouldBe(20);
    }

    [Fact]
    public async Task Pda_NestedParentheses_PositionAdvancesCorrectly()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("((()))");

        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        var startHtml = await startResp.Content.ReadAsStringAsync();
        ExtractPosition(startHtml).ShouldBe(0);

        var stepModel = BuildBalancedParenthesesPda("((()))");
        stepModel.HasExecuted = true;
        stepModel.CurrentStateId = 1;

        var step1 = await PostAsync(client, "/AutomatonExecution/StepForward", stepModel);
        var step1Html = await step1.Content.ReadAsStringAsync();
        ExtractPosition(step1Html).ShouldBe(1);
        ExtractNextSymbolHighlight(step1Html).ShouldBe('(');
    }

    [Fact]
    public async Task Pda_InvalidInput_PreservesInputThroughRejection()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("(((");

        var execResp = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var execHtml = await execResp.Content.ReadAsStringAsync();
        ExtractInputValue(execHtml).ShouldBe("(((");
        ExtractPosition(execHtml).ShouldBe(3);
    }

    [Fact]
    public async Task Pda_EpsilonTransition_NoSymbolHighlightDuringEpsilon()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda("");

        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        var startHtml = await startResp.Content.ReadAsStringAsync();
        ExtractPosition(startHtml).ShouldBe(0);
        var highlightSpans = CountSymbolHighlightSpans(startHtml);
        highlightSpans.ShouldBe(0);
    }

    [Fact]
    public async Task Pda_MultipleSteps_InputRemainsConstant()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()()");

        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        var startHtml = await startResp.Content.ReadAsStringAsync();
        ExtractInputValue(startHtml).ShouldBe("()()");

        var stepModel = BuildBalancedParenthesesPda("()()");
        stepModel.HasExecuted = true;
        stepModel.CurrentStateId = 1;

        for (int i = 0; i < 4; i++)
        {
            var stepResp = await PostAsync(client, "/AutomatonExecution/StepForward", stepModel);
            var stepHtml = await stepResp.Content.ReadAsStringAsync();
            ExtractInputValue(stepHtml).ShouldBe("()()");
            stepModel.CurrentStateId = 1;
        }
    }

    [Fact]
    public async Task Pda_AlternatingSymbols_HighlightsCorrectly()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");

        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        var startHtml = await startResp.Content.ReadAsStringAsync();
        ExtractNextSymbolHighlight(startHtml).ShouldBe('(');

        var stepModel = BuildBalancedParenthesesPda("()");
        stepModel.HasExecuted = true;
        stepModel.CurrentStateId = 1;

        var step1Resp = await PostAsync(client, "/AutomatonExecution/StepForward", stepModel);
        var step1Html = await step1Resp.Content.ReadAsStringAsync();
        ExtractNextSymbolHighlight(step1Html).ShouldBe(')');
    }

    [Fact]
    public async Task Pda_EmptyInput_NoSymbolHighlight()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = "",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        var startHtml = await startResp.Content.ReadAsStringAsync();
        ExtractInputValue(startHtml).ShouldBe(string.Empty);
        CountSymbolHighlightSpans(startHtml).ShouldBe(0);
    }

    [Fact]
    public async Task Pda_ComplexInput_PositionTracksCorrectly()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda("aaabbb");

        var execResp = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var execHtml = await execResp.Content.ReadAsStringAsync();
        ExtractInputValue(execHtml).ShouldBe("aaabbb");
        ExtractPosition(execHtml).ShouldBe(6);
    }

    [Fact]
    public async Task Pda_PartialExecution_InputPreserved()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("(())");

        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        _ = await startResp.Content.ReadAsStringAsync();

        var stepModel = BuildBalancedParenthesesPda("(())");
        stepModel.HasExecuted = true;
        stepModel.CurrentStateId = 1;

        var step1Resp = await PostAsync(client, "/AutomatonExecution/StepForward", stepModel);
        var step1Html = await step1Resp.Content.ReadAsStringAsync();
        ExtractInputValue(step1Html).ShouldBe("(())");
        ExtractPosition(step1Html).ShouldBe(1);

        stepModel.CurrentStateId = 1;
        stepModel.Position = 1; // Update position from previous step
        var step2Resp = await PostAsync(client, "/AutomatonExecution/StepForward", stepModel);
        var step2Html = await step2Resp.Content.ReadAsStringAsync();
        ExtractInputValue(step2Html).ShouldBe("(())");
        ExtractPosition(step2Html).ShouldBe(2);
    }

    [Fact]
    public async Task Pda_MismatchedParentheses_InputPreservedOnRejection()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("())");

        var execResp = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);
        var execHtml = await execResp.Content.ReadAsStringAsync();
        ExtractInputValue(execHtml).ShouldBe("())");
    }

    [Fact]
    public async Task Pda_AfterResetThenStart_InputBehaviorCorrect()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");

        // Execute
        await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);

        // Reset
        model.HasExecuted = true;
        var resetResp = await PostAsync(client, "/AutomatonExecution/Reset", model);
        var resetHtml = await resetResp.Content.ReadAsStringAsync();
        InputIsReadonly(resetHtml).ShouldBeFalse();

        var newModel = BuildBalancedParenthesesPda("(())");
        var startResp = await PostAsync(client, "/AutomatonExecution/Start", newModel);
        var startHtml = await startResp.Content.ReadAsStringAsync();
        ExtractInputValue(startHtml).ShouldBe("(())");
        InputIsReadonly(startHtml).ShouldBeTrue();
    }

    [Fact]
    public async Task Pda_SingleChar_InputHandledCorrectly()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" }],
            Input = "a",
            IsCustomAutomaton = true,
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        var startHtml = await startResp.Content.ReadAsStringAsync();
        ExtractInputValue(startHtml).ShouldBe("a");
        ExtractNextSymbolHighlight(startHtml).ShouldBe('a');
    }

    [Fact]
    public async Task Pda_VeryLongInput_HandledCorrectly()
    {
        var client = GetHttpClient();
        var longInput = string.Concat(Enumerable.Repeat("()", 50));
        var model = BuildBalancedParenthesesPda(longInput);

        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        var startHtml = await startResp.Content.ReadAsStringAsync();
        ExtractInputValue(startHtml).ShouldBe(longInput);
        ExtractPosition(startHtml).ShouldBe(0);
    }

    [Fact]
    public async Task Pda_InputWithSpecialChars_PreservedCorrectly()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");

        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        var startHtml = await startResp.Content.ReadAsStringAsync();
        var inputValue = ExtractInputValue(startHtml);
        inputValue.ShouldContain('(');
        inputValue.ShouldContain(')');
    }

    [Fact]
    public async Task Pda_AfterBackToStart_CanStepForwardAgain()
    {
        var client = GetHttpClient();
        var model = BuildBalancedParenthesesPda("()");

        // Execute partially
        _ = await PostAsync(client, "/AutomatonExecution/Start", model);
        var stepModel = BuildBalancedParenthesesPda("()");
        stepModel.HasExecuted = true;
        stepModel.CurrentStateId = 1;
        await PostAsync(client, "/AutomatonExecution/StepForward", stepModel);

        // BackToStart
        stepModel.Position = 1;
        var backResp = await PostAsync(client, "/AutomatonExecution/BackToStart", stepModel);
        var backHtml = await backResp.Content.ReadAsStringAsync();
        ExtractPosition(backHtml).ShouldBe(0);

        // StepForward again
        stepModel.Position = 0;
        var step2Resp = await PostAsync(client, "/AutomatonExecution/StepForward", stepModel);
        var step2Html = await step2Resp.Content.ReadAsStringAsync();
        ExtractPosition(step2Html).ShouldBe(1);
        ExtractInputValue(step2Html).ShouldBe("()");
    }

    [Fact]
    public async Task Pda_ConsecutiveSteps_PositionIncrements()
    {
        var client = GetHttpClient();
        var model = BuildAnBnPda("aabb");
        _ = await PostAsync(client, "/AutomatonExecution/Start", model);

        var stepModel = BuildAnBnPda("aabb");
        stepModel.HasExecuted = true;
        stepModel.CurrentStateId = 1;

        var step1 = await PostAsync(client, "/AutomatonExecution/StepForward", stepModel);
        var step1Html = await step1.Content.ReadAsStringAsync();
        ExtractPosition(step1Html).ShouldBe(1);

        stepModel.CurrentStateId = 1;
        stepModel.Position = 1; // Update position from previous step
        stepModel.StackSerialized = ExtractStackSerialized(step1Html); // Preserve stack state
        stepModel.StateHistorySerialized = ExtractStateHistorySerialized(step1Html); // Preserve history
        var step2 = await PostAsync(client, "/AutomatonExecution/StepForward", stepModel);
        var step2Html = await step2.Content.ReadAsStringAsync();
        ExtractPosition(step2Html).ShouldBe(2);

        stepModel.CurrentStateId = 1;
        stepModel.Position = 2; // Update position from previous step
        stepModel.StackSerialized = ExtractStackSerialized(step2Html); // Preserve stack state
        stepModel.StateHistorySerialized = ExtractStateHistorySerialized(step2Html); // Preserve history
        var step3 = await PostAsync(client, "/AutomatonExecution/StepForward", stepModel);
        var step3Html = await step3.Content.ReadAsStringAsync();
        ExtractPosition(step3Html).ShouldBe(3);
    }
}

