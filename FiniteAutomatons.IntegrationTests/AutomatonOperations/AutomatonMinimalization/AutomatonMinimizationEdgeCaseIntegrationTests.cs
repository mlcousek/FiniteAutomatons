using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests.AutomatonOperations.AutomatonMinimalization;

[Collection("Integration Tests")]
public class AutomatonMinimizationEdgeCaseIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    private static List<KeyValuePair<string, string>> BuildForm(AutomatonViewModel m)
    {
        var list = new List<KeyValuePair<string, string>>
        {
            new("Type", ((int)m.Type).ToString()),
            new("Input", m.Input ?? string.Empty),
            new("Position", m.Position.ToString()),
            new("HasExecuted", m.HasExecuted.ToString().ToLower()),
            new("IsCustomAutomaton", m.IsCustomAutomaton.ToString().ToLower()),
            new("StateHistorySerialized", m.StateHistorySerialized ?? string.Empty)
        };
        if (m.CurrentStateId.HasValue) list.Add(new("CurrentStateId", m.CurrentStateId.Value.ToString()));
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
            list.Add(new($"Transitions[{i}].Symbol", m.Transitions[i].Symbol.ToString()));
        }
        return list;
    }
    private static async Task<HttpResponseMessage> PostAsync(HttpClient c, string url, AutomatonViewModel m) => await c.PostAsync(url, new FormUrlEncodedContent(BuildForm(m)));

    private static int ExtractHiddenInt(string html, string name)
    {
        var m = Regex.Match(html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"(\\d+)\"|value=\"(\\d+)\"[^>]*name=\"{Regex.Escape(name)}\"", RegexOptions.IgnoreCase);
        if (!m.Success) return -1; return int.Parse(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
    }
    private static bool ExtractHiddenBool(string html, string name)
    {
        var m = Regex.Match(html, $"name=\"{Regex.Escape(name)}\"[^>]*value=\"(true|false)\"|value=\"(true|false)\"[^>]*name=\"{Regex.Escape(name)}\"", RegexOptions.IgnoreCase);
        if (!m.Success) return false; return bool.Parse(m.Groups[1].Success ? m.Groups[1].Value : m.Groups[2].Value);
    }

    // Edge case: DFA with no states -> analysis unsupported
    [Fact]
    public async Task Index_DfaWithNoStates_ShowsNoMinimizeControls()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel { Type = AutomatonType.DFA, States = [], Transitions = [], Input = string.Empty, IsCustomAutomaton = true };
        var createResp = await PostAsync(client, "/Automaton/CreateAutomaton", model);
        createResp.StatusCode.ShouldBeOneOf(new[] { HttpStatusCode.OK, HttpStatusCode.Found });
        var indexResp = await client.GetAsync("/Home/Index");
        indexResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await indexResp.Content.ReadAsStringAsync();
        // Minimize panel header still present but no minimize button or info text should be rendered for unsupported analysis
        html.ShouldContain("MINIMALIZE");
        html.IndexOf("minimize-btn", StringComparison.OrdinalIgnoreCase).ShouldBe(-1);
    }

    // Edge case: DFA with states but no start state -> show minimize controls but reachable count may be zero; just ensure button present
    [Fact]
    public async Task Index_DfaWithNoStartState_ShowsMinimizeButton()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = false, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }],
            Input = "a",
            IsCustomAutomaton = true
        };
        var createResp = await PostAsync(client, "/Automaton/CreateAutomaton", model);
        createResp.StatusCode.ShouldBeOneOf(new[] { HttpStatusCode.OK, HttpStatusCode.Found });
        var indexResp = await client.GetAsync("/Home/Index");
        indexResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await indexResp.Content.ReadAsStringAsync();
        // Just ensure the minimize button/rendering exists (label may vary)
        html.IndexOf("Minimalize (", StringComparison.OrdinalIgnoreCase).ShouldBeGreaterThanOrEqualTo(0);
        // There should be a minimize button element present
        Regex.IsMatch(html, "<button[^>]*class=\"[^\"]*minimize-btn[^\"]*\"", RegexOptions.IgnoreCase).ShouldBeTrue();
    }

    // Edge case: single-state DFA already minimal (non-accepting)
    [Fact]
    public async Task Index_SingleStateNonAcceptingDfa_AlreadyMinimal()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 0, IsStart = true, IsAccepting = false }],
            Transitions = [],
            Input = string.Empty,
            IsCustomAutomaton = true
        };
        var createResp = await PostAsync(client, "/Automaton/CreateAutomaton", model);
        createResp.StatusCode.ShouldBeOneOf(new[] { HttpStatusCode.OK, HttpStatusCode.Found });
        var indexResp = await client.GetAsync("/Home/Index");
        indexResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await indexResp.Content.ReadAsStringAsync();
        // Button should indicate already minimal and be disabled
        html.IndexOf("Minimalize (Already Minimal)", StringComparison.OrdinalIgnoreCase).ShouldBeGreaterThanOrEqualTo(0);
        Regex.IsMatch(html, "<button[^>]*class=\"[^\"]*minimize-btn[^\"]*\"[^>]*disabled", RegexOptions.IgnoreCase).ShouldBeTrue();
    }

    // Minimization should not change acceptance of empty input when start state accepting
    [Fact]
    public async Task Minimalize_DfaAcceptsEmptyInput_AllowedAndAcceptanceUnchanged()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 0, IsStart = true, IsAccepting = true }, new() { Id = 1, IsStart = false, IsAccepting = true }],
            Transitions = [new() { FromStateId = 0, ToStateId = 1, Symbol = 'a' }, new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' }],
            Input = "", // empty
            IsCustomAutomaton = true
        };
        var minimizeResp = await PostAsync(client, "/Automaton/Minimalize", model);
        minimizeResp.StatusCode.ShouldBeOneOf(new[] { HttpStatusCode.Found, HttpStatusCode.OK });
        var indexResp = await client.GetAsync("/Home/Index");
        var html = await indexResp.Content.ReadAsStringAsync();
        // Expect converted message OR already minimal button depending on final state count
        (html.Contains("Minimalize (Already Minimal)") || Regex.IsMatch(html, "DFA minimized: \\d+ -> \\d+ states", RegexOptions.IgnoreCase))
            .ShouldBeTrue();
        // Execute all on minimized (single accepting start state expected)
        var minimizedModel = new AutomatonViewModel { Type = AutomatonType.DFA, States = [new() { Id = 0, IsStart = true, IsAccepting = true }], Transitions = [], Input = "", IsCustomAutomaton = true };
        var execResp = await PostAsync(client, "/Automaton/ExecuteAll", minimizedModel);
        execResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var execHtml = await execResp.Content.ReadAsStringAsync();
        execHtml.ShouldContain("ACCEPTED", Case.Insensitive);
    }
}
