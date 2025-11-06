using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests;

[Collection("Integration Tests")]
public class AutomatonExecutionDfaTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task Start_OnDfa_SetsCurrentStatePositionZero()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("ab");
        var resp = await PostAsync(client, "/Automaton/Start", model);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var parsed = await DeserializeAsync(resp);
        parsed.Position.ShouldBe(0);
        parsed.CurrentStateId.ShouldBe(1);
        parsed.IsAccepted.ShouldBeNull();
        parsed.HasExecuted.ShouldBeTrue();
    }

    [Fact]
    public async Task StepForward_IncrementsPositionAndMovesState()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("ab");
        var start = await DeserializeAsync(await PostAsync(client, "/Automaton/Start", model));
        var forwardResp = await PostAsync(client, "/Automaton/StepForward", start);
        forwardResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var forward = await DeserializeAsync(forwardResp);
        forward.Position.ShouldBe(1);
        forward.CurrentStateId.ShouldBe(2);
        forward.IsAccepted.ShouldBeNull();
    }

    [Fact]
    public async Task StepBackward_DecrementsPositionRestoresState()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("ab");
        var start = await DeserializeAsync(await PostAsync(client, "/Automaton/Start", model));
        var forward = await DeserializeAsync(await PostAsync(client, "/Automaton/StepForward", start));
        forward.Position.ShouldBe(1);
        var backResp = await PostAsync(client, "/Automaton/StepBackward", forward);
        backResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var back = await DeserializeAsync(backResp);
        back.Position.ShouldBe(0);
        back.CurrentStateId.ShouldBe(1);
        back.IsAccepted.ShouldBeNull();
    }

    [Fact]
    public async Task ExecuteAll_SetsEndPositionAndAcceptance()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("ab");
        var resp = await PostAsync(client, "/Automaton/ExecuteAll", model);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var parsed = await DeserializeAsync(resp);
        parsed.Position.ShouldBe(parsed.Input!.Length);
        parsed.CurrentStateId.ShouldBe(2);
        parsed.IsAccepted.ShouldNotBeNull();
        parsed.IsAccepted!.Value.ShouldBeTrue();
    }

    [Fact]
    public async Task BackToStart_AfterExecuteAll_ResetsPositionClearsAcceptance()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("ab");
        var exec = await DeserializeAsync(await PostAsync(client, "/Automaton/ExecuteAll", model));
        exec.IsAccepted.ShouldNotBeNull();
        exec.IsAccepted!.Value.ShouldBeTrue();
        var backResp = await PostAsync(client, "/Automaton/BackToStart", exec);
        backResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var back = await DeserializeAsync(backResp);
        back.Position.ShouldBe(0);
        back.CurrentStateId.ShouldBe(1);
        back.IsAccepted.ShouldBeNull();
    }

    [Fact]
    public async Task Reset_ClearsExecutionStatePreservesStructure()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("ab");
        var exec = await DeserializeAsync(await PostAsync(client, "/Automaton/ExecuteAll", model));
        var resetResp = await PostAsync(client, "/Automaton/Reset", exec);
        resetResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var reset = await DeserializeAsync(resetResp);
        reset.Input.ShouldBe(string.Empty);
        reset.Position.ShouldBe(0);
        reset.IsAccepted.ShouldBeNull();
        reset.CurrentStateId.ShouldBeNull();
        reset.States.Count.ShouldBe(model.States.Count);
        reset.Transitions.Count.ShouldBe(model.Transitions.Count);
    }

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

    private static async Task<HttpResponseMessage> PostAsync(HttpClient client, string url, AutomatonViewModel model)
    {
        var form = BuildForm(model);
        return await client.PostAsync(url, new FormUrlEncodedContent(form));
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

    private static async Task<AutomatonViewModel> DeserializeAsync(HttpResponseMessage resp)
    {
        var html = await resp.Content.ReadAsStringAsync();
        return Deserialize(html);
    }

    private static AutomatonViewModel Deserialize(string html)
    {
        var vm = new AutomatonViewModel
        {
            States = new List<FiniteAutomatons.Core.Models.DoMain.State>(),
            Transitions = new List<FiniteAutomatons.Core.Models.DoMain.Transition>()
        };
        vm.Type = (AutomatonType)ParseInt(html, "Type", (int)AutomatonType.DFA);
        vm.Position = ParseInt(html, "Position", 0);
        vm.CurrentStateId = ParseIntNullable(html, "CurrentStateId");
        vm.HasExecuted = ParseBool(html, "HasExecuted") ?? false;
        vm.IsAccepted = ParseBool(html, "IsAccepted");
        vm.Input = ParseValue(html, "id=\"inputField\"", "value") ?? string.Empty;
        vm.StateHistorySerialized = ParseValue(html, "name=\"StateHistorySerialized\"", "value") ?? string.Empty;

        var stateIds = Regex.Matches(html, "States\\[(\\d+)\\]\\.Id\"[^>]*value=\"(\\d+)\"");
        var starts = Regex.Matches(html, "States\\[(\\d+)\\]\\.IsStart\"[^>]*value=\"(true|false)\"");
        var accepts = Regex.Matches(html, "States\\[(\\d+)\\]\\.IsAccepting\"[^>]*value=\"(true|false)\"");
        for (int i=0;i<stateIds.Count;i++)
        {
            var id = int.Parse(stateIds[i].Groups[2].Value);
            bool isStart = i < starts.Count && bool.Parse(starts[i].Groups[2].Value);
            bool isAccept = i < accepts.Count && bool.Parse(accepts[i].Groups[2].Value);
            vm.States.Add(new() { Id=id, IsStart=isStart, IsAccepting=isAccept });
        }
        var froms = Regex.Matches(html, "Transitions\\[(\\d+)\\]\\.FromStateId\"[^>]*value=\"(\\d+)\"");
        var tos = Regex.Matches(html, "Transitions\\[(\\d+)\\]\\.ToStateId\"[^>]*value=\"(\\d+)\"");
        var syms = Regex.Matches(html, "Transitions\\[(\\d+)\\]\\.Symbol\"[^>]*value=\"(.)\"");
        for (int i=0;i<froms.Count && i<tos.Count;i++)
        {
            var from = int.Parse(froms[i].Groups[2].Value);
            var to = int.Parse(tos[i].Groups[2].Value);
            var sym = i<syms.Count ? syms[i].Groups[2].Value[0] : '\0';
            vm.Transitions.Add(new() { FromStateId=from, ToStateId=to, Symbol=sym });
        }
        return vm;
    }

    private static string? ExtractInputValue(string html, string name)
    {
        var first = Regex.Match(html, $"<input[^>]*name=\"{name}\"[^>]*value=\"([^\"]*)\"", RegexOptions.IgnoreCase);
        if (first.Success) return first.Groups[1].Value;
        var second = Regex.Match(html, $"<input[^>]*value=\"([^\"]*)\"[^>]*name=\"{name}\"", RegexOptions.IgnoreCase);
        return second.Success ? second.Groups[1].Value : null;
    }

    private static int ParseInt(string html, string name, int def)
    {
        var val = ExtractInputValue(html, name);
        return int.TryParse(val, out var i) ? i : def;
    }
    private static int? ParseIntNullable(string html, string name)
    {
        var val = ExtractInputValue(html, name);
        return int.TryParse(val, out var i) ? i : null;
    }
    private static bool? ParseBool(string html, string name)
    {
        var val = ExtractInputValue(html, name);
        return bool.TryParse(val, out var b) ? b : null;
    }
    private static string? ParseValue(string html, string token, string attr)
    {
        var m = Regex.Match(html, token + "[^>]*" + attr + "=\"([^\"]*)\"", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : null;
    }
}
