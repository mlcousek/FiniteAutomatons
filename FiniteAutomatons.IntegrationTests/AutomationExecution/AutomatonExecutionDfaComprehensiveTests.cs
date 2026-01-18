using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests.AutomationExecution;

[Collection("Integration Tests")]
public class AutomatonExecutionDfaComprehensiveTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    // DFA: q1 -- d -> q2 (accepting), q2 -- a -> q3 (accepting)
    private static AutomatonViewModel BuildThreeStateDfa(string input) => new()
    {
        Type = AutomatonType.DFA,
        States =
        [
            new() { Id = 1, IsStart = true, IsAccepting = false },
            new() { Id = 2, IsStart = false, IsAccepting = true },
            new() { Id = 3, IsStart = false, IsAccepting = true }
        ],
        Transitions =
        [
            new() { FromStateId = 1, ToStateId = 2, Symbol = 'd' },
            new() { FromStateId = 2, ToStateId = 3, Symbol = 'a' }
        ],
        Input = input,
        IsCustomAutomaton = true
    };

    [Fact]
    public async Task MultiCharacterInput_PreservedAcrossStartAndSteps()
    {
        var client = GetHttpClient();
        var model = BuildThreeStateDfa("da");

        var startHtml = await (await PostAsync(client, "/AutomatonExecution/Start", model)).Content.ReadAsStringAsync();
        ExtractInput(startHtml).ShouldBe("da");
        ExtractPosition(startHtml).pos.ShouldBe(0);
        ExtractNextSymbol(startHtml).ShouldBe('d');

        var startModel = Deserialize(startHtml);
        var step1Html = await (await PostAsync(client, "/AutomatonExecution/StepForward", startModel)).Content.ReadAsStringAsync();
        ExtractInput(step1Html).ShouldBe("da");
        ExtractPosition(step1Html).pos.ShouldBe(1);
        ExtractNextSymbol(step1Html).ShouldBe('a');

        var step1Model = Deserialize(step1Html);
        var step2Html = await (await PostAsync(client, "/AutomatonExecution/StepForward", step1Model)).Content.ReadAsStringAsync();
        ExtractInput(step2Html).ShouldBe("da");
        ExtractPosition(step2Html).pos.ShouldBe(2);
        // End of input -> no next symbol highlight; expect placeholder text instead
        HasNextSymbol(step2Html).ShouldBeFalse();
    }

    [Fact]
    public async Task StepBackward_RestoresPreviousSymbol()
    {
        var client = GetHttpClient();
        var model = BuildThreeStateDfa("da");
        var start = Deserialize(await (await PostAsync(client, "/AutomatonExecution/Start", model)).Content.ReadAsStringAsync());
        var step1 = Deserialize(await (await PostAsync(client, "/AutomatonExecution/StepForward", start)).Content.ReadAsStringAsync());
        step1.Position.ShouldBe(1);
        step1.CurrentStateId.ShouldBe(2);
        var backHtml = await (await PostAsync(client, "/AutomatonExecution/StepBackward", step1)).Content.ReadAsStringAsync();
        ExtractPosition(backHtml).pos.ShouldBe(0);
        ExtractNextSymbol(backHtml).ShouldBe('d');
    }

    [Fact]
    public async Task ExecuteAll_EndsExecution_NoNextSymbol_ShowsResult()
    {
        var client = GetHttpClient();
        var model = BuildThreeStateDfa("da");
        var resp = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await resp.Content.ReadAsStringAsync();
        var (pos, len) = ExtractPosition(html);
        pos.ShouldBe(len);
        HasNextSymbol(html).ShouldBeFalse();
        html.ShouldContain("Result:");
        html.ShouldContain("ACCEPTED", Case.Insensitive);
    }

    [Fact]
    public async Task ExecuteAll_RejectedScenario()
    {
        // Modify DFA so second symbol mismatches (input "db")
        var client = GetHttpClient();
        var model = BuildThreeStateDfa("db"); // transitions require d then a -> second char b should cause rejection
        var resp = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await resp.Content.ReadAsStringAsync();
        var (pos, len) = ExtractPosition(html);
        pos.ShouldBe(len);
        html.ShouldContain("Result:");
        html.ShouldContain("REJECTED", Case.Insensitive);
    }

    [Fact]
    public async Task BackToStart_AfterPartialProgress_RestoresNextSymbol()
    {
        var client = GetHttpClient();
        var model = BuildThreeStateDfa("da");
        var start = Deserialize(await (await PostAsync(client, "/AutomatonExecution/Start", model)).Content.ReadAsStringAsync());
        var step1 = Deserialize(await (await PostAsync(client, "/AutomatonExecution/StepForward", start)).Content.ReadAsStringAsync());
        step1.Position.ShouldBe(1);
        var backHtml = await (await PostAsync(client, "/AutomatonExecution/BackToStart", step1)).Content.ReadAsStringAsync();
        ExtractPosition(backHtml).pos.ShouldBe(0);
        ExtractNextSymbol(backHtml).ShouldBe('d');
    }

    [Fact]
    public async Task Reset_ClearsInputAndExecution()
    {
        var client = GetHttpClient();
        var model = BuildThreeStateDfa("da");
        var execHtml = await (await PostAsync(client, "/AutomatonExecution/ExecuteAll", model)).Content.ReadAsStringAsync();
        ExtractPosition(execHtml).pos.ShouldBe(2);
        var execModel = Deserialize(execHtml);
        var resetHtml = await (await PostAsync(client, "/AutomatonExecution/Reset", execModel)).Content.ReadAsStringAsync();
        ExtractInput(resetHtml).ShouldBe(string.Empty);
        ExtractPosition(resetHtml).pos.ShouldBe(0);
        HasNextSymbol(resetHtml).ShouldBeFalse();
    }

    // ---------------- Helpers ----------------
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

    private static AutomatonViewModel Deserialize(string html)
    {
        var vm = new AutomatonViewModel
        {
            States = new List<Core.Models.DoMain.State>(),
            Transitions = new List<Core.Models.DoMain.Transition>()
        };
        vm.Type = (AutomatonType)ParseInt(html, "Type", (int)AutomatonType.DFA);
        vm.Position = ParseInt(html, "Position", 0);
        vm.CurrentStateId = ParseIntNullable(html, "CurrentStateId");
        vm.HasExecuted = ParseBool(html, "HasExecuted") ?? false;
        vm.IsAccepted = ParseBool(html, "IsAccepted");
        vm.Input = ExtractInput(html) ?? string.Empty;
        vm.StateHistorySerialized = ExtractInputValue(html, "StateHistorySerialized") ?? string.Empty;

        var stateIds = Regex.Matches(html, "States\\[(\\d+)\\]\\.Id\"[^>]*value=\"(\\d+)\"");
        var starts = Regex.Matches(html, "States\\[(\\d+)\\]\\.IsStart\"[^>]*value=\"(true|false)\"");
        var accepts = Regex.Matches(html, "States\\[(\\d+)\\]\\.IsAccepting\"[^>]*value=\"(true|false)\"");
        for (int i = 0; i < stateIds.Count; i++)
        {
            var id = int.Parse(stateIds[i].Groups[2].Value);
            bool isStart = i < starts.Count && bool.Parse(starts[i].Groups[2].Value);
            bool isAccept = i < accepts.Count && bool.Parse(accepts[i].Groups[2].Value);
            vm.States.Add(new() { Id = id, IsStart = isStart, IsAccepting = isAccept });
        }
        var froms = Regex.Matches(html, "Transitions\\[(\\d+)\\]\\.FromStateId\"[^>]*value=\"(\\d+)\"");
        var tos = Regex.Matches(html, "Transitions\\[(\\d+)\\]\\.ToStateId\"[^>]*value=\"(\\d+)\"");
        var syms = Regex.Matches(html, "Transitions\\[(\\d+)\\]\\.Symbol\"[^>]*value=\"(.)\"");
        for (int i = 0; i < froms.Count && i < tos.Count; i++)
        {
            var from = int.Parse(froms[i].Groups[2].Value);
            var to = int.Parse(tos[i].Groups[2].Value);
            var sym = i < syms.Count ? syms[i].Groups[2].Value[0] : '\0';
            vm.Transitions.Add(new() { FromStateId = from, ToStateId = to, Symbol = sym });
        }
        return vm;
    }

    private static string? ExtractInput(string html)
        => ExtractInputValue(html, "Input");
    private static (int pos, int len) ExtractPosition(string html)
    {
        var val = ExtractInputValue(html, "Position");
        var input = ExtractInput(html) ?? string.Empty;
        int pos = int.TryParse(val, out var p) ? p : 0;
        return (pos, input.Length);
    }
    private static char ExtractNextSymbol(string html)
    {
        var m = Regex.Match(html, @"<span[^>]*class=""[^""]*\bsymbol-highlight\b[^""]*""[^>]*>'(?<c>.)'</span", RegexOptions.IgnoreCase | RegexOptions.Singleline);
        m.Success.ShouldBeTrue("Expected next symbol highlight present");
        var captured = m.Groups["c"].Value;
        return captured.Length > 0 ? captured[0] : '\0';
    }
    private static bool HasNextSymbol(string html)
        => Regex.IsMatch(html, "class=\"symbol-highlight\"", RegexOptions.IgnoreCase);

    private static string? ExtractInputValue(string html, string name)
    {
        var first = Regex.Match(html, $"<input[^>]*name=\"{name}\"[^>]*value=\"([^\"]*)\"", RegexOptions.IgnoreCase);
        if (first.Success) return first.Groups[1].Value;
        var second = Regex.Match(html, $"<input[^>]*value=\"([^\"]*)\"[^>]*name=\"{name}\"", RegexOptions.IgnoreCase);
        return second.Success ? second.Groups[1].Value : null;
    }

    private static int ParseInt(string html, string name, int def)
    {
        var v = ExtractInputValue(html, name);
        return int.TryParse(v, out var i) ? i : def;
    }
    private static int? ParseIntNullable(string html, string name)
    {
        var v = ExtractInputValue(html, name);
        return int.TryParse(v, out var i) ? i : null;
    }
    private static bool? ParseBool(string html, string name)
    {
        var v = ExtractInputValue(html, name);
        return bool.TryParse(v, out var b) ? b : null;
    }
}
