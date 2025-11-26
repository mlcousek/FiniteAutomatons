using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests.AutomatonOperations.AutomatonEditing;

[Collection("Integration Tests")]
public class AutomatonEditingAndEdgeCaseTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    private static AutomatonViewModel BaseDfa(string input = "") => new()
    {
        Type = AutomatonType.DFA,
        States =
        [
            new() { Id = 1, IsStart = true, IsAccepting = false }
        ],
        Transitions = [],
        Input = input,
        IsCustomAutomaton = true
    };

    private static AutomatonViewModel NfaMulti(string input) => new()
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
            new() { FromStateId = 1, ToStateId = 3, Symbol = 'a' }
        ],
        Input = input,
        IsCustomAutomaton = true
    };

    // ---------- Helpers ----------
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, AutomatonViewModel model, Dictionary<string, string>? extra = null)
    {
        var data = BuildForm(model);
        if (extra != null)
            foreach (var kv in extra) data.Add(new(kv.Key, kv.Value));
        return await client.PostAsync(url, new FormUrlEncodedContent(data));
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
            new("StateHistorySerialized", m.StateHistorySerialized ?? string.Empty)
        };
        if (m.CurrentStateId.HasValue) list.Add(new("CurrentStateId", m.CurrentStateId.Value.ToString()));
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
            list.Add(new($"Transitions[{i}].Symbol", m.Transitions[i].Symbol.ToString()));
        }
        return list;
    }

    private static int CountHiddenStates(string html) => Regex.Matches(html, "name=\"States\\.Index\"", RegexOptions.IgnoreCase).Count;
    private static int CountHiddenTransitions(string html) => Regex.Matches(html, "name=\"Transitions\\[(\\d+)\\]\\.FromStateId\"", RegexOptions.IgnoreCase).Count;

    // ---------- Tests ----------

    [Fact]
    public async Task StepBackward_AtStart_DoesNotChangePosition()
    {
        var client = GetHttpClient();
        var model = BaseDfa("a");
        var startHtml = await (await PostAsync(client, "/Automaton/Start", model)).Content.ReadAsStringAsync();
        Regex.IsMatch(startHtml, "name=\"Position\"[^>]*value=\"0\"", RegexOptions.IgnoreCase).ShouldBeTrue();
        // attempt backward
        var startModel = BaseDfa("a");
        startModel.HasExecuted = true; startModel.CurrentStateId = 1; // state after start
        var backResp = await PostAsync(client, "/Automaton/StepBackward", startModel);
        var backHtml = await backResp.Content.ReadAsStringAsync();
        Regex.IsMatch(backHtml, "name=\"Position\"[^>]*value=\"0\"", RegexOptions.IgnoreCase).ShouldBeTrue();
    }

    [Fact]
    public async Task Nfa_StepForward_ShouldShowMultipleCurrentStates()
    {
        var client = GetHttpClient();
        var model = NfaMulti("a");
        var startHtml = await (await PostAsync(client, "/Automaton/Start", model)).Content.ReadAsStringAsync();
        // Execute one step
        var startModel = NfaMulti("a");
        startModel.HasExecuted = true; // mark execution started
        var stepResp = await PostAsync(client, "/Automaton/StepForward", startModel);
        stepResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var stepHtml = await stepResp.Content.ReadAsStringAsync();
        // Should contain hidden inputs for CurrentStates[0] and CurrentStates[1]
        Regex.Matches(stepHtml, "name=\"CurrentStates\\[\\d+\\]\"[^>]*value=\"(\\d+)\"", RegexOptions.IgnoreCase).Count.ShouldBeGreaterThan(1);
    }

    [Fact]
    public async Task ExportJson_ShouldReturnJsonWithStates()
    {
        var client = GetHttpClient();
        var model = BaseDfa("ab");
        model.States.Add(new() { Id = 2, IsAccepting = true });
        var resp = await PostAsync(client, "/Automaton/ExportJson", model);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        var json = await resp.Content.ReadAsStringAsync();
        json.ShouldContain("\"States\"", Case.Insensitive);
        json.ShouldContain("\"Id\": 1");
        json.ShouldContain("\"Id\": 2");
    }

    [Fact]
    public async Task ExportText_ShouldReturnPlainText()
    {
        var client = GetHttpClient();
        var model = BaseDfa("ab");
        model.States.Add(new() { Id = 2, IsAccepting = true });
        model.Transitions.Add(new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' });
        var resp = await PostAsync(client, "/Automaton/ExportText", model);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
        var txt = await resp.Content.ReadAsStringAsync();
        txt.ShouldContain("q0");
        txt.ShouldContain("q1");
        txt.ShouldContain("a");
    }
}
