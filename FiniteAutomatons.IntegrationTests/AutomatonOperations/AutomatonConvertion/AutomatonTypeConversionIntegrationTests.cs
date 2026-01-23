//using FiniteAutomatons.Core.Models.ViewModel;
//using Shouldly;
//using System.Net;
//using System.Text.RegularExpressions;

//namespace FiniteAutomatons.IntegrationTests.AutomatonOperations.AutomatonConvertion;

//[Collection("Integration Tests")]
//public class AutomatonTypeConversionIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
//{
//    private static AutomatonViewModel BuildEpsilonNfa(string input) => new()
//    {
//        Type = AutomatonType.EpsilonNFA,
//        States =
//        [
//            new() { Id = 1, IsStart = true, IsAccepting = false },
//            new() { Id = 2, IsStart = false, IsAccepting = true },
//            new() { Id = 3, IsStart = false, IsAccepting = false }
//        ],
//        Transitions =
//        [
//            // epsilon from 1 -> 2 makes start accepting after conversion
//            new() { FromStateId = 1, ToStateId = 2, Symbol = 'ε' },
//            // symbol transitions
//            new() { FromStateId = 1, ToStateId = 3, Symbol = 'a' },
//            new() { FromStateId = 3, ToStateId = 2, Symbol = 'b' }
//        ],
//        Input = input,
//        IsCustomAutomaton = true
//    };

//    private static async Task<HttpResponseMessage> PostSwitchTypeAsync(HttpClient client, AutomatonViewModel model, AutomatonType targetType)
//    {
//        var form = BuildForm(model);
//        form.Add(new("targetType", ((int)targetType).ToString()));
//        return await client.PostAsync("/AutomatonConversion/SwitchType", new FormUrlEncodedContent(form));
//    }

//    private static List<KeyValuePair<string, string>> BuildForm(AutomatonViewModel m)
//    {
//        var list = new List<KeyValuePair<string, string>>
//        {
//            new("Type", ((int)m.Type).ToString()),
//            new("Input", m.Input ?? string.Empty),
//            new("Position", m.Position.ToString()),
//            new("HasExecuted", m.HasExecuted.ToString().ToLower()),
//            new("IsCustomAutomaton", m.IsCustomAutomaton.ToString().ToLower()),
//            new("StateHistorySerialized", m.StateHistorySerialized ?? string.Empty)
//        };
//        if (m.CurrentStateId.HasValue) list.Add(new("CurrentStateId", m.CurrentStateId.Value.ToString()));
//        for (int i = 0; i < m.States.Count; i++)
//        {
//            list.Add(new("States.Index", i.ToString()));
//            list.Add(new($"States[{i}].Id", m.States[i].Id.ToString()));
//            list.Add(new($"States[{i}].IsStart", m.States[i].IsStart.ToString().ToLower()));
//            list.Add(new($"States[{i}].IsAccepting", m.States[i].IsAccepting.ToString().ToLower()));
//        }
//        for (int i = 0; i < m.Transitions.Count; i++)
//        {
//            list.Add(new("Transitions.Index", i.ToString()));
//            list.Add(new($"Transitions[{i}].FromStateId", m.Transitions[i].FromStateId.ToString()));
//            list.Add(new($"Transitions[{i}].ToStateId", m.Transitions[i].ToStateId.ToString()));
//            list.Add(new($"Transitions[{i}].Symbol", m.Transitions[i].Symbol.ToString()));
//        }
//        return list;
//    }

//    private static int ParseInt(string html, string name, int def)
//    {
//        var m = Regex.Match(html, $"<input[^>]*name=\"{name}\"[^>]*value=\"(\\d+)\"", RegexOptions.IgnoreCase);
//        return m.Success && int.TryParse(m.Groups[1].Value, out var v) ? v : def;
//    }

//    private static IEnumerable<char> ExtractTransitionSymbols(string html)
//        => Regex.Matches(html, "Transitions\\[(\\d+)\\]\\.Symbol\"[^>]*value=\"(.)\"")
//                 .Cast<Match>()
//                 .Select(m => m.Groups[2].Value[0]);

//    private static IEnumerable<(int Id, bool IsStart, bool IsAccepting)> ExtractStates(string html)
//    {
//        var ids = Regex.Matches(html, "States\\[(\\d+)\\]\\.Id\"[^>]*value=\"(\\d+)\"");
//        var starts = Regex.Matches(html, "States\\[(\\d+)\\]\\.IsStart\"[^>]*value=\"(true|false)\"");
//        var accepts = Regex.Matches(html, "States\\[(\\d+)\\]\\.IsAccepting\"[^>]*value=\"(true|false)\"");
//        for (int i = 0; i < ids.Count; i++)
//        {
//            var id = int.Parse(ids[i].Groups[2].Value);
//            var isStart = bool.Parse(starts[i].Groups[2].Value);
//            var isAccepting = bool.Parse(accepts[i].Groups[2].Value);
//            yield return (id, isStart, isAccepting);
//        }
//    }

//    [Fact] //TODO : repair
//    public async Task SwitchType_EpsilonNfaToNfa_RemovesEpsilonTransitionsAndUpdatesAccepting()
//    {
//        var client = GetHttpClient();
//        var model = BuildEpsilonNfa("");
//        var resp = await PostSwitchTypeAsync(client, model, AutomatonType.NFA);
//        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
//        var html = await resp.Content.ReadAsStringAsync();

//        // Type should be NFA (enum int 1)
//        ParseInt(html, "Type", -1).ShouldBe((int)AutomatonType.NFA);

//        // No epsilon transitions
//        ExtractTransitionSymbols(html).ShouldAllBe(s => s != '\0');

//        // Start state should become accepting via epsilon closure (state 1 had epsilon to accepting 2)
//        var states = ExtractStates(html).ToList();
//        var (Id, IsStart, IsAccepting) = states.First(s => s.IsStart);
//        IsAccepting.ShouldBeTrue();
//    }

//    [Fact] //TODO : repair
//    public async Task SwitchType_EpsilonNfaToDfa_ProducesDfaType()
//    {
//        var client = GetHttpClient();
//        var model = BuildEpsilonNfa("ab");
//        var resp = await PostSwitchTypeAsync(client, model, AutomatonType.DFA);
//        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
//        var html = await resp.Content.ReadAsStringAsync();
//        ParseInt(html, "Type", -1).ShouldBe((int)AutomatonType.DFA);
//    }

//    [Fact]
//    public async Task SwitchType_NfaToDfa_AllowedAndChangesType()
//    {
//        var client = GetHttpClient();
//        var nfaModel = new AutomatonViewModel
//        {
//            Type = AutomatonType.NFA,
//            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
//            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }],
//            Input = "a",
//            IsCustomAutomaton = true
//        };
//        var resp = await PostSwitchTypeAsync(client, nfaModel, AutomatonType.DFA);
//        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
//        var html = await resp.Content.ReadAsStringAsync();
//        ParseInt(html, "Type", -1).ShouldBe((int)AutomatonType.DFA);
//    }

//    [Fact] //TODO : repair
//    public async Task SwitchType_EpsilonNfaToNfa_FallbackPathStillRemovesEpsilon_WhenBuilderFails()
//    {
//        // Simulate failure by sending malformed model (no start state) which could cause CreateAutomatonFromModel to fail.
//        var client = GetHttpClient();
//        var model = new AutomatonViewModel
//        {
//            Type = AutomatonType.EpsilonNFA,
//            States = [new() { Id = 2, IsStart = false, IsAccepting = true }],
//            Transitions = [new() { FromStateId = 2, ToStateId = 2, Symbol = 'ε' }],
//            IsCustomAutomaton = true
//        };
//        var resp = await PostSwitchTypeAsync(client, model, AutomatonType.NFA);
//        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
//        var html = await resp.Content.ReadAsStringAsync();
//        ParseInt(html, "Type", -1).ShouldBe((int)AutomatonType.NFA);
//        ExtractTransitionSymbols(html).ShouldAllBe(s => s != '\0');
//    }
//}
