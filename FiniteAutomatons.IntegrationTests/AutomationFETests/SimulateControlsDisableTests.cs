using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.IntegrationTests.AutomatonOperations.AutomatonGeneration;
using Shouldly;
using System.Net;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests.AutomationFETests;

[Collection("Integration Tests")]
public class SimulateControlsDisableTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
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

    private static string ExtractButtonBlock(string html) => Regex.Match(html, "<div class=\"control-content simulate-buttons\"[\\s\\S]*?</div>").Value;
    private static bool ButtonDisabled(string block, string actionName)
        => Regex.IsMatch(block, $"data-sim-action=\"{actionName}\"[^>]*disabled", RegexOptions.IgnoreCase);
    private static bool ButtonPresent(string block, string actionName)
        => Regex.IsMatch(block, $"data-sim-action=\"{actionName}\"", RegexOptions.IgnoreCase);

    [Fact]
    public async Task BeforeStart_AllActionButtonsDisabled()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("ab");
        // Render initial index by posting create (redirect then GET Index)
        var resp = await PostAsync(client, "/Automaton/CreateAutomaton", model);
        resp.StatusCode.ShouldBeOneOf(new[]{HttpStatusCode.OK, HttpStatusCode.Found});
        var html = await resp.Content.ReadAsStringAsync();
        var block = ExtractButtonBlock(html);
        ButtonPresent(block, "backToStart").ShouldBeTrue();
        ButtonPresent(block, "stepBackward").ShouldBeTrue();
        ButtonPresent(block, "stepForward").ShouldBeTrue();
        ButtonPresent(block, "executeAll").ShouldBeTrue();
        ButtonDisabled(block, "backToStart").ShouldBeTrue();
        ButtonDisabled(block, "stepBackward").ShouldBeTrue();
        ButtonDisabled(block, "stepForward").ShouldBeTrue();
        ButtonDisabled(block, "executeAll").ShouldBeTrue();
    }

    [Fact]
    public async Task AfterStart_AtPositionZero_BackAndStepBackwardDisabled_OthersEnabled()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("ab");
        var startResp = await PostAsync(client, "/Automaton/Start", model);
        startResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await startResp.Content.ReadAsStringAsync();
        var block = ExtractButtonBlock(html);
        ButtonDisabled(block, "backToStart").ShouldBeTrue();
        ButtonDisabled(block, "stepBackward").ShouldBeTrue();
        ButtonDisabled(block, "stepForward").ShouldBeFalse();
        ButtonDisabled(block, "executeAll").ShouldBeFalse();
    }

    [Fact]
    public async Task AfterOneStep_BackAndStepBackwardEnabled()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("ab");
        var startHtml = await (await PostAsync(client, "/Automaton/Start", model)).Content.ReadAsStringAsync();
        // Build model for step forward
        var stepModel = BuildSimpleDfa("ab");
        stepModel.HasExecuted = true; stepModel.CurrentStateId = 1; // current state before consuming 'a'
        var stepResp = await PostAsync(client, "/Automaton/StepForward", stepModel);
        stepResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await stepResp.Content.ReadAsStringAsync();
        var block = ExtractButtonBlock(html);
        ButtonDisabled(block, "backToStart").ShouldBeFalse();
        ButtonDisabled(block, "stepBackward").ShouldBeFalse();
    }

    [Fact]
    public async Task AtEndPosition_StepForwardAndExecuteAllDisabled_BackEnabled()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("ab");
        var execResp = await PostAsync(client, "/Automaton/ExecuteAll", model);
        execResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await execResp.Content.ReadAsStringAsync();
        var block = ExtractButtonBlock(html);
        ButtonDisabled(block, "stepForward").ShouldBeTrue();
        ButtonDisabled(block, "executeAll").ShouldBeTrue();
        ButtonDisabled(block, "backToStart").ShouldBeFalse();
    }

    [Fact]
    public async Task BackToStart_AfterEnd_DisablesBackAndStepBackwardAgain()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("ab");
        var endHtml = await (await PostAsync(client, "/Automaton/ExecuteAll", model)).Content.ReadAsStringAsync();
        var endModel = BuildSimpleDfa("ab");
        endModel.HasExecuted = true; endModel.Position = 2; endModel.CurrentStateId = 2;
        var backResp = await PostAsync(client, "/Automaton/BackToStart", endModel);
        backResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await backResp.Content.ReadAsStringAsync();
        var block = ExtractButtonBlock(html);
        ButtonDisabled(block, "backToStart").ShouldBeTrue();
        ButtonDisabled(block, "stepBackward").ShouldBeTrue();
        ButtonDisabled(block, "stepForward").ShouldBeFalse();
        ButtonDisabled(block, "executeAll").ShouldBeFalse();
    }

    [Fact]
    public async Task Reset_DisablesAllAgainUntilStart()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDfa("ab");
        var execHtml = await (await PostAsync(client, "/Automaton/ExecuteAll", model)).Content.ReadAsStringAsync();
        var execModel = BuildSimpleDfa("ab");
        execModel.HasExecuted = true; execModel.Position = 2; execModel.CurrentStateId = 2;
        var resetResp = await PostAsync(client, "/Automaton/Reset", execModel);
        resetResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await resetResp.Content.ReadAsStringAsync();
        var block = ExtractButtonBlock(html);
        ButtonDisabled(block, "backToStart").ShouldBeTrue();
        ButtonDisabled(block, "stepBackward").ShouldBeTrue();
        ButtonDisabled(block, "stepForward").ShouldBeTrue();
        ButtonDisabled(block, "executeAll").ShouldBeTrue();
    }
}
