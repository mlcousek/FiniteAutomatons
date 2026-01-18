using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;
using System.Text;

namespace FiniteAutomatons.IntegrationTests.AutomatonApiTests;

[Collection("Integration Tests")]
public class AutomatonFileEndpointsTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task ImportAutomaton_DfaJson_Works()
    {
        var client = GetHttpClient();
        var json = "{\n  \"Version\":1,\n  \"Type\":\"DFA\",\n  \"States\":[{\"Id\":1,\"IsStart\":true,\"IsAccepting\":false},{\"Id\":2,\"IsStart\":false,\"IsAccepting\":true}],\n  \"Transitions\":[{\"FromStateId\":1,\"ToStateId\":2,\"Symbol\":\"a\"}]\n}";
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes(json)), "upload", "dfa.json" }
        };
        var response = await client.PostAsync("/ImportExport/ImportAutomaton", content);
        // Should redirect to Home/Index on success
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        // Should have loaded the automaton
        html.ShouldContain("AUTOMATON");
    }

    [Fact]
    public async Task ExportJson_ReturnsFile()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }]
        };
        var form = new List<KeyValuePair<string, string>>
        {
            new("Type", model.Type.ToString()),
            new("States[0].Id","1"), new("States[0].IsStart","true"), new("States[0].IsAccepting","false"),
            new("States[1].Id","2"), new("States[1].IsStart","false"), new("States[1].IsAccepting","true"),
            new("Transitions[0].FromStateId","1"), new("Transitions[0].ToStateId","2"), new("Transitions[0].Symbol","a")
        };
        var resp = await client.PostAsync("/ImportExport/ExportJson", new FormUrlEncodedContent(form));
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task ExportText_ReturnsFile()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }, new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' }]
        };
        var form = new List<KeyValuePair<string, string>>
        {
            new("Type", model.Type.ToString()),
            new("States[0].Id","1"), new("States[0].IsStart","true"), new("States[0].IsAccepting","false"),
            new("States[1].Id","2"), new("States[1].IsStart","false"), new("States[1].IsAccepting","true"),
            new("Transitions[0].FromStateId","1"), new("Transitions[0].ToStateId","2"), new("Transitions[0].Symbol","a"),
            new("Transitions[1].FromStateId","1"), new("Transitions[1].ToStateId","1"), new("Transitions[1].Symbol","a")
        };
        var resp = await client.PostAsync("/ImportExport/ExportText", new FormUrlEncodedContent(form));
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
    }
}
