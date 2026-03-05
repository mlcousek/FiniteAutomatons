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
        var createResp = await PostAsync(client, "/AutomatonCreation/CreateAutomaton", model);
        createResp.StatusCode.ShouldBeOneOf([HttpStatusCode.OK, HttpStatusCode.Found]);

        if (createResp.StatusCode == HttpStatusCode.OK)
        {
            var createHtml = await createResp.Content.ReadAsStringAsync();
            createHtml.ShouldContain("Automaton");
            return;
        }

        var indexResp = await client.GetAsync("/Home/Index");
        indexResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await indexResp.Content.ReadAsStringAsync();
        html.IndexOf("Minimalize (Already Minimal)", StringComparison.OrdinalIgnoreCase).ShouldBeGreaterThanOrEqualTo(0);
        Regex.IsMatch(html, "<button[^>]*class=\"[^\"]*minimize-btn[^\"]*\"[^>]*disabled", RegexOptions.IgnoreCase).ShouldBeTrue();
    }
}
