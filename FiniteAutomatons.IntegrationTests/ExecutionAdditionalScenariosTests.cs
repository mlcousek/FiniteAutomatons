using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests;

[Collection("Integration Tests")]
public class ExecutionAdditionalScenariosTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    private static AutomatonViewModel AcceptsEmptyDfa() => new()
    {
        Type = AutomatonType.DFA,
        States = [ new() { Id = 1, IsStart = true, IsAccepting = true } ],
        Transitions = [],
        Input = string.Empty,
        IsCustomAutomaton = true
    };

    private static AutomatonViewModel SimpleNfa(string input) => new()
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
        Input = input,
        IsCustomAutomaton = true
    };

    private static AutomatonViewModel UnknownSymbolDfa(string input) => new()
    {
        Type = AutomatonType.DFA,
        States =
        [
            new() { Id = 1, IsStart = true, IsAccepting = false },
            new() { Id = 2, IsStart = false, IsAccepting = true }
        ],
        Transitions = [ new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' } ], // only 'a' known
        Input = input,
        IsCustomAutomaton = true
    };

    // ------------ Helpers ------------
    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, AutomatonViewModel model)
    {
        var data = BuildForm(model);
        return await client.PostAsync(url, new FormUrlEncodedContent(data));
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
            int i=0; foreach(var s in m.CurrentStates){ list.Add(new("CurrentStates.Index", i.ToString())); list.Add(new($"CurrentStates[{i}]", s.ToString())); i++; }
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

    private static int ExtractPosition(string html) => Regex.Match(html, "name=\"Position\"[^>]*value=\"(\\d+)\"", RegexOptions.IgnoreCase) is var m && m.Success ? int.Parse(m.Groups[1].Value) : 0;
    private static bool? ExtractIsAccepted(string html) => Regex.Match(html, "name=\"IsAccepted\"[^>]*value=\"(true|false)\"", RegexOptions.IgnoreCase) is var m && m.Success ? bool.Parse(m.Groups[1].Value) : (bool?)null;

    // ------------ Tests ------------

    [Fact]
    public async Task EmptyInput_Dfa_AcceptsImmediately()
    {
        var client = GetHttpClient();
        var model = AcceptsEmptyDfa();
        var resp = await PostAsync(client, "/Automaton/ExecuteAll", model);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await resp.Content.ReadAsStringAsync();
        ExtractPosition(html).ShouldBe(0); // input length 0
        var accepted = ExtractIsAccepted(html);
        accepted.HasValue.ShouldBeTrue();
        accepted!.Value.ShouldBeTrue();
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task Nfa_NonDeterministicAcceptance()
    {
        var client = GetHttpClient();
        var model = SimpleNfa("a");
        var resp = await PostAsync(client, "/Automaton/ExecuteAll", model);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await resp.Content.ReadAsStringAsync();
        var accepted = ExtractIsAccepted(html);
        accepted.HasValue.ShouldBeTrue();
        accepted!.Value.ShouldBeTrue(); // one path leads to accepting state 2
        html.ShouldContain("Result:");
    }

    [Fact]
    public async Task UnknownSymbol_Dfa_ShouldReject()
    {
        var client = GetHttpClient();
        var model = UnknownSymbolDfa("b"); // no 'b' transition => cannot move => rejection after execute all
        var resp = await PostAsync(client, "/Automaton/ExecuteAll", model);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await resp.Content.ReadAsStringAsync();
        var accepted = ExtractIsAccepted(html);
        accepted.HasValue.ShouldBeTrue();
        accepted!.Value.ShouldBeFalse();
        html.ShouldContain("REJECTED", Case.Insensitive);
    }

    [Fact]
    public async Task MultiStepForwardBackwardSequence_Dfa_TracksHistory()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [ new() { Id = 1, IsStart = true }, new() { Id = 2, IsAccepting = true } ],
            Transitions = [ new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }, new() { FromStateId = 2, ToStateId = 2, Symbol = 'b' } ],
            Input = "ab",
            IsCustomAutomaton = true
        };

        var startHtml = await (await PostAsync(client, "/Automaton/Start", model)).Content.ReadAsStringAsync();
        ExtractPosition(startHtml).ShouldBe(0);
        // Forward twice
        var startModel = new AutomatonViewModel
        {
            Type = model.Type,
            States = model.States.Select(s => new FiniteAutomatons.Core.Models.DoMain.State { Id=s.Id, IsStart=s.IsStart, IsAccepting=s.IsAccepting }).ToList(),
            Transitions = model.Transitions.Select(t => new FiniteAutomatons.Core.Models.DoMain.Transition { FromStateId=t.FromStateId, ToStateId=t.ToStateId, Symbol=t.Symbol }).ToList(),
            Input = model.Input,
            IsCustomAutomaton = true,
            HasExecuted = true,
            CurrentStateId = 1,
            Position = 0
        };
        var step1Html = await (await PostAsync(client, "/Automaton/StepForward", startModel)).Content.ReadAsStringAsync();
        ExtractPosition(step1Html).ShouldBe(1);
        var step1Model = new AutomatonViewModel
        {
            Type = model.Type,
            States = model.States.Select(s => new FiniteAutomatons.Core.Models.DoMain.State { Id=s.Id, IsStart=s.IsStart, IsAccepting=s.IsAccepting }).ToList(),
            Transitions = model.Transitions.Select(t => new FiniteAutomatons.Core.Models.DoMain.Transition { FromStateId=t.FromStateId, ToStateId=t.ToStateId, Symbol=t.Symbol }).ToList(),
            Input = model.Input,
            IsCustomAutomaton = true,
            HasExecuted = true,
            CurrentStateId = 2,
            Position = 1
        };
        var step2Html = await (await PostAsync(client, "/Automaton/StepForward", step1Model)).Content.ReadAsStringAsync();
        ExtractPosition(step2Html).ShouldBe(2);
        // Backward one
        var backModel = new AutomatonViewModel
        {
            Type = model.Type,
            States = model.States.Select(s => new FiniteAutomatons.Core.Models.DoMain.State { Id=s.Id, IsStart=s.IsStart, IsAccepting=s.IsAccepting }).ToList(),
            Transitions = model.Transitions.Select(t => new FiniteAutomatons.Core.Models.DoMain.Transition { FromStateId=t.FromStateId, ToStateId=t.ToStateId, Symbol=t.Symbol }).ToList(),
            Input = model.Input,
            IsCustomAutomaton = true,
            HasExecuted = true,
            CurrentStateId = 2,
            Position = 2
        };
        var backHtml = await (await PostAsync(client, "/Automaton/StepBackward", backModel)).Content.ReadAsStringAsync();
        ExtractPosition(backHtml).ShouldBe(1);
        // Inspect history serialized hidden input presence
        Regex.IsMatch(step2Html, "name=\"StateHistorySerialized\"", RegexOptions.IgnoreCase).ShouldBeTrue();
        Regex.IsMatch(backHtml, "name=\"StateHistorySerialized\"", RegexOptions.IgnoreCase).ShouldBeTrue();
    }
}
