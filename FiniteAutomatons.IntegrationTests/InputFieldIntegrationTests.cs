using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests;

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

    private static List<KeyValuePair<string,string>> BuildForm(AutomatonViewModel m)
    {
        var list = new List<KeyValuePair<string,string>>
        {
            new("Type", ((int)m.Type).ToString()),
            new("Input", m.Input ?? string.Empty),
            new("Position", m.Position.ToString()),
            new("HasExecuted", m.HasExecuted.ToString().ToLower()),
            new("IsCustomAutomaton", m.IsCustomAutomaton.ToString().ToLower()),
            new("StateHistorySerialized", m.StateHistorySerialized ?? string.Empty)
        };
        if (m.CurrentStateId.HasValue) list.Add(new("CurrentStateId", m.CurrentStateId.Value.ToString()));
        if (m.CurrentStates != null)
        {
            int i = 0;
            foreach(var s in m.CurrentStates){
                list.Add(new("CurrentStates.Index", i.ToString()));
                list.Add(new($"CurrentStates[{i}]", s.ToString()));
                i++;}
        }
        for (int i=0;i<m.States.Count;i++)
        {
            list.Add(new("States.Index", i.ToString()));
            list.Add(new($"States[{i}].Id", m.States[i].Id.ToString()));
            list.Add(new($"States[{i}].IsStart", m.States[i].IsStart.ToString().ToLower()));
            list.Add(new($"States[{i}].IsAccepting", m.States[i].IsAccepting.ToString().ToLower()));
        }
        for (int i=0;i<m.Transitions.Count;i++)
        {
            list.Add(new("Transitions.Index", i.ToString()));
            list.Add(new($"Transitions[{i}].FromStateId", m.Transitions[i].FromStateId.ToString()));
            list.Add(new($"Transitions[{i}].ToStateId", m.Transitions[i].ToStateId.ToString()));
            list.Add(new($"Transitions[{i}].Symbol", m.Transitions[i].Symbol.ToString()));
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
        return m.Success ? m.Groups["c"].Value[0] : (char?)null;
    }

    private static int CountSymbolHighlightSpans(string html)
    {
        return Regex.Matches(html, @"<span[^>]*class=""symbol-highlight""", RegexOptions.IgnoreCase | RegexOptions.Singleline).Count;
    }

    [Fact]
    public async Task InputField_BeforeStart_ShouldBeEditable()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("abba");
        var resp = await PostAsync(client, "/Automaton/CreateAutomaton", model); // creation may return OK if validation passes
        resp.StatusCode.ShouldBeOneOf(new[]{HttpStatusCode.OK, HttpStatusCode.Found});
        var html = await resp.Content.ReadAsStringAsync();
        ExtractInputValue(html).ShouldBe("abba");
        InputIsReadonly(html).ShouldBeFalse();
    }

    [Fact]
    public async Task Start_ShouldPreserveFullInputAndMakeReadonly()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("abba");
        var startResp = await PostAsync(client, "/Automaton/Start", model);
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
        var start = await PostAsync(client, "/Automaton/Start", model);
        var startModelHtml = await start.Content.ReadAsStringAsync();
        ExtractPosition(startModelHtml).ShouldBe(0);
        var startModel = BuildSimpleDfa("abba"); // reconstruct minimal for step forward
        startModel.HasExecuted = true;
        // Provide execution state fields from html if needed (CurrentStateId)
        var csMatch = Regex.Match(startModelHtml, "name=\"CurrentStateId\"[^>]*value=\"(\\d+)\"", RegexOptions.IgnoreCase);
        if (csMatch.Success) startModel.CurrentStateId = int.Parse(csMatch.Groups[1].Value);
        var stepResp = await PostAsync(client, "/Automaton/StepForward", startModel);
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
        var execHtml = await (await PostAsync(client, "/Automaton/ExecuteAll", model)).Content.ReadAsStringAsync();
        ExtractPosition(execHtml).ShouldBe(4);
        var execModel = BuildSimpleDfa("abba");
        execModel.HasExecuted = true;
        execModel.Position = 4;
        var csMatch = Regex.Match(execHtml, "name=\"CurrentStateId\"[^>]*value=\"(\\d+)\"", RegexOptions.IgnoreCase);
        if (csMatch.Success) execModel.CurrentStateId = int.Parse(csMatch.Groups[1].Value);
        var backResp = await PostAsync(client, "/Automaton/BackToStart", execModel);
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
        var resp = await PostAsync(client, "/Automaton/ExecuteAll", model);
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
        var execHtml = await (await PostAsync(client, "/Automaton/ExecuteAll", model)).Content.ReadAsStringAsync();
        var execModel = BuildSimpleDfa("ab");
        execModel.HasExecuted = true;
        execModel.Position = 2;
        var resetResp = await PostAsync(client, "/Automaton/Reset", execModel);
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
        var startResp = await PostAsync(client, "/Automaton/Start", model);
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
        var model = BuildSimpleDfa("acb"); // transitions only cover 'a'/'b' but start does not consume yet
        var startResp = await PostAsync(client, "/Automaton/Start", model);
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
        // custom DFA allowing c,a,d,c sequence
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [ new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true } ],
            Transitions = [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'c' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'd' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'c' }
            ],
            Input = "cadc",
            IsCustomAutomaton = true
        };
        var startResp = await PostAsync(client, "/Automaton/Start", model);
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
            States = [ new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true } ],
            Transitions = [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'c' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'd' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'c' }
            ],
            Input = "cadc",
            IsCustomAutomaton = true
        };
        var startResp = await PostAsync(client, "/Automaton/Start", model);
        startResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var startHtml = await startResp.Content.ReadAsStringAsync();
        ExtractPosition(startHtml).ShouldBe(0);
        // prepare step model
        var stepModel = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = model.States,
            Transitions = model.Transitions,
            Input = model.Input,
            IsCustomAutomaton = true,
            HasExecuted = true,
            CurrentStateId = 2 // after consuming first 'c' per transition definition
        };
        var stepResp = await PostAsync(client, "/Automaton/StepForward", stepModel);
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
        var resp = await PostAsync(client, "/Automaton/Start", model);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await resp.Content.ReadAsStringAsync();
        var value = ExtractInputValue(html);
        value.ShouldBe("acb");
        // Only one highlight span expected
        CountSymbolHighlightSpans(html).ShouldBe(1);
        ExtractNextSymbolHighlight(html).ShouldBe('a');
        // Ensure other characters still present in input (means they are shown; overlay JS not executed in test)
        value.Contains('c').ShouldBeTrue();
        value.Contains('b').ShouldBeTrue();
    }

    [Fact]
    public async Task StepForward_ShouldShowFullInputAndOtherChars_Unhighlighted_For_acb()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("acb");
        var start = await PostAsync(client, "/Automaton/Start", model);
        start.StatusCode.ShouldBe(HttpStatusCode.OK);
        // Prepare step model (execution started)
        var stepModel = BuildSimpleDfa("acb");
        stepModel.HasExecuted = true;
        stepModel.CurrentStateId = 2; // after consuming 'a'
        var stepResp = await PostAsync(client, "/Automaton/StepForward", stepModel);
        stepResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await stepResp.Content.ReadAsStringAsync();
        var value = ExtractInputValue(html);
        value.ShouldBe("acb");
        // Because 'c' is not part of the DFA's defined transition alphabet, no symbol highlight span is rendered.
        CountSymbolHighlightSpans(html).ShouldBe(0);
        ExtractNextSymbolHighlight(html).ShouldBeNull();
        // Remaining chars appear in input string
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
            States = [ new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true } ],
            Transitions = [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'c' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'd' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'c' }
            ],
            Input = "cadc",
            IsCustomAutomaton = true
        };
        var resp = await PostAsync(client, "/Automaton/Start", model);
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
            States = [ new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true } ],
            Transitions = [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'c' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'd' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'c' }
            ],
            Input = "cadc",
            IsCustomAutomaton = true
        };
        var start = await PostAsync(client, "/Automaton/Start", model);
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
        var stepResp = await PostAsync(client, "/Automaton/StepForward", stepModel);
        stepResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await stepResp.Content.ReadAsStringAsync();
        var value = ExtractInputValue(html);
        value.ShouldBe("cadc");
        CountSymbolHighlightSpans(html).ShouldBe(1);
        ExtractNextSymbolHighlight(html).ShouldBe('a');
        value.Contains('d').ShouldBeTrue();
        value.LastIndexOf('c').ShouldBeGreaterThan(0);
    }
}
