using System.Net; 
using System.Text; 
using Shouldly; 
using FiniteAutomatons.Core.Models.ViewModel; 

namespace FiniteAutomatons.IntegrationTests.AutomatonApiTests;

[Collection("Integration Tests")]
public class AutomatonFileEndpointsTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    [Fact]
    public async Task ImportAutomaton_DfaJson_Works()
    {
        var client = GetHttpClient();
        var json = "{\n  \"Version\":1,\n  \"Type\":\"DFA\",\n  \"States\":[{\"Id\":1,\"IsStart\":true,\"IsAccepting\":false},{\"Id\":2,\"IsStart\":false,\"IsAccepting\":true}],\n  \"Transitions\":[{\"FromStateId\":1,\"ToStateId\":2,\"Symbol\":\"a\"}]\n}";
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(json)), "upload", "dfa.json");
        var response = await client.PostAsync("/Automaton/ImportAutomaton", content);
        response.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
    }

    [Fact]
    public async Task ImportAutomaton_Invalid_ReturnsViewWithError()
    {
        var client = GetHttpClient();
        var json = "{ \"Version\":1, \"States\":[], \"Transitions\":[] }"; // invalid (no states)
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(json)), "upload", "bad.json");
        var response = await client.PostAsync("/Automaton/ImportAutomaton", content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK); // returns view with model errors
        var body = await response.Content.ReadAsStringAsync();
        body.ToLowerInvariant().ShouldContain("state");
    }

    [Fact]
    public async Task ExportJson_ReturnsFile()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [ new(){ Id=1, IsStart=true, IsAccepting=false}, new(){ Id=2, IsStart=false, IsAccepting=true } ],
            Transitions = [ new(){ FromStateId=1, ToStateId=2, Symbol='a'} ]
        };
        var form = new List<KeyValuePair<string,string>>
        {
            new("Type", model.Type.ToString()),
            new("States[0].Id","1"), new("States[0].IsStart","true"), new("States[0].IsAccepting","false"),
            new("States[1].Id","2"), new("States[1].IsStart","false"), new("States[1].IsAccepting","true"),
            new("Transitions[0].FromStateId","1"), new("Transitions[0].ToStateId","2"), new("Transitions[0].Symbol","a")
        };
        var resp = await client.PostAsync("/Automaton/ExportJson", new FormUrlEncodedContent(form));
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
            States = [ new(){ Id=1, IsStart=true, IsAccepting=false}, new(){ Id=2, IsStart=false, IsAccepting=true } ],
            Transitions = [ new(){ FromStateId=1, ToStateId=2, Symbol='a'}, new(){ FromStateId=1, ToStateId=1, Symbol='a'} ]
        };
        var form = new List<KeyValuePair<string,string>>
        {
            new("Type", model.Type.ToString()),
            new("States[0].Id","1"), new("States[0].IsStart","true"), new("States[0].IsAccepting","false"),
            new("States[1].Id","2"), new("States[1].IsStart","false"), new("States[1].IsAccepting","true"),
            new("Transitions[0].FromStateId","1"), new("Transitions[0].ToStateId","2"), new("Transitions[0].Symbol","a"),
            new("Transitions[1].FromStateId","1"), new("Transitions[1].ToStateId","1"), new("Transitions[1].Symbol","a")
        };
        var resp = await client.PostAsync("/Automaton/ExportText", new FormUrlEncodedContent(form));
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
    }
}
