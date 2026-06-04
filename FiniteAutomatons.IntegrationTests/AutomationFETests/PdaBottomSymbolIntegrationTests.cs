using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.IntegrationTests.AutomationFETests;

[Collection("Integration Tests")]
public class PdaBottomSymbolIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    private static AutomatonViewModel BuildSimpleDpda(string input, char bottomSymbol) => new()
    {
        Type = AutomatonType.DPDA,
        States =
        [
            new() { Id = 1, IsStart = true, IsAccepting = false },
            new() { Id = 2, IsStart = false, IsAccepting = true }
        ],
        Transitions =
        [
            new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = bottomSymbol, StackPush = null }
        ],
        Input = input,
        IsCustomAutomaton = true,
        BottomSymbol = bottomSymbol,
        AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly
    };

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
            new("AcceptanceMode", ((int)m.AcceptanceMode).ToString()),
            new("BottomSymbol", m.BottomSymbol == '\0' ? "#" : m.BottomSymbol.ToString())
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

    private static string ExtractBottomSymbolValue(string html)
    {
        var m = Regex.Match(html, "id=\"bottomSymbolDisplayField\"[^>]*value=\"([^\"]*)\"", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    private static string ExtractHiddenBottomSymbolValue(string html)
    {
        var m = Regex.Match(html, "name=\"BottomSymbol\"[^>]*value=\"([^\"]*)\"", RegexOptions.IgnoreCase);
        return m.Success ? m.Groups[1].Value : string.Empty;
    }

    [Fact]
    public async Task Start_CustomBottomSymbol_ShouldPreserveSymbolInUI()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDpda("a", 'Z');

        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        startResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var html = await startResp.Content.ReadAsStringAsync();
        
        ExtractBottomSymbolValue(html).ShouldBe("Z");
        ExtractHiddenBottomSymbolValue(html).ShouldBe("Z");
    }

    [Fact]
    public async Task StepForward_CustomBottomSymbol_ShouldPreserveSymbolInUI()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDpda("a", 'Z');

        var startResp = await PostAsync(client, "/AutomatonExecution/Start", model);
        startResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var startHtml = await startResp.Content.ReadAsStringAsync();
        
        var stepModel = BuildSimpleDpda("a", 'Z');
        stepModel.HasExecuted = true;
        var csMatch = Regex.Match(startHtml, "name=\"CurrentStateId\"[^>]*value=\"(\\d+)\"", RegexOptions.IgnoreCase);
        if (csMatch.Success) stepModel.CurrentStateId = int.Parse(csMatch.Groups[1].Value);

        var stepResp = await PostAsync(client, "/AutomatonExecution/StepForward", stepModel);
        stepResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var stepHtml = await stepResp.Content.ReadAsStringAsync();
        
        ExtractBottomSymbolValue(stepHtml).ShouldBe("Z");
        ExtractHiddenBottomSymbolValue(stepHtml).ShouldBe("Z");
    }

    [Fact]
    public async Task ExecuteAll_CustomBottomSymbol_ShouldPreserveSymbolInUI()
    {
        var client = GetHttpClient();
        var model = BuildSimpleDpda("a", 'X');

        var startResp = await PostAsync(client, "/AutomatonExecution/ExecuteAll", model);
        startResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        
        var html = await startResp.Content.ReadAsStringAsync();
        
        ExtractBottomSymbolValue(html).ShouldBe("X");
    }

    [Fact]
    public void FinalStateAndEmptyStack_ShouldRejectIfStackNotEmpty()
    {
        var dpda = new DPDA { AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack };
        dpda.States.Add(new State { Id = 0, IsStart = true, IsAccepting = false });
        dpda.States.Add(new State { Id = 1, IsStart = false, IsAccepting = true });
        // Transition pop epsilon, push epsilon
        dpda.Transitions.Add(new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'b', StackPop = '\0', StackPush = "" });
        
        var stack = new Stack<char>(new[] { '#', 'A' }); // Push # then A -> A is on top
        var execState = dpda.StartExecution("b", stack);
        dpda.ExecuteAll(execState);
        
        execState.IsAccepted.ShouldBe(false);
    }

    [Fact]
    public void FinalStateAndEmptyStack_NPDA_ShouldRejectIfStackNotEmpty()
    {
        var npda = new NPDA { AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack };
        npda.States.Add(new State { Id = 0, IsStart = true, IsAccepting = false });
        npda.States.Add(new State { Id = 1, IsStart = false, IsAccepting = true });
        npda.Transitions.Add(new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'b', StackPop = '\0', StackPush = "" });
        
        var stack = new Stack<char>(new[] { '#', 'A' }); // Push # then A -> A is on top
        var execState = npda.StartExecution("b", stack);
        npda.ExecuteAll(execState);
        
        execState.IsAccepted.ShouldBe(false);
    }
}
