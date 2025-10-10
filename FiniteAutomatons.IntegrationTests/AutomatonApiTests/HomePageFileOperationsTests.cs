using System.Net; 
using System.Text; 
using Shouldly; 

namespace FiniteAutomatons.IntegrationTests.AutomatonApiTests;

[Collection("Integration Tests")]
public class HomePageFileOperationsTests(IntegrationTestsFixture f) : IntegrationTestsBase(f)
{
    [Fact]
    public async Task Home_ImportAutomaton_FromIndexPage_Works()
    {
        var client = GetHttpClient();
        // first get home to establish any cookies
        var home = await client.GetAsync("/");
        home.EnsureSuccessStatusCode();

        var json = "{\n  \"Version\":1,\n  \"Type\":\"DFA\",\n  \"States\":[{\"Id\":1,\"IsStart\":true,\"IsAccepting\":false},{\"Id\":2,\"IsStart\":false,\"IsAccepting\":true}],\n  \"Transitions\":[{\"FromStateId\":1,\"ToStateId\":2,\"Symbol\":\"a\"}]\n}";
        using var mp = new MultipartFormDataContent();
        mp.Add(new ByteArrayContent(Encoding.UTF8.GetBytes(json)), "upload", "dfa.json");
        var resp = await client.PostAsync("/Automaton/ImportAutomaton", mp);
        resp.StatusCode.ShouldBeOneOf(HttpStatusCode.OK, HttpStatusCode.Redirect);
        // follow redirect if any
        if (resp.StatusCode == HttpStatusCode.Redirect && resp.Headers.Location != null)
        {
            var follow = await client.GetAsync(resp.Headers.Location);
            follow.EnsureSuccessStatusCode();
            var body = await follow.Content.ReadAsStringAsync();
            body.ShouldContain("Custom Automaton");
        }
    }

    [Fact]
    public async Task Home_ExportJson_ReturnsFile()
    {
        var client = GetHttpClient();
        // Build form representing a small DFA
        var form = new List<KeyValuePair<string,string>>
        {
            new("Type","DFA"),
            new("States[0].Id","1"), new("States[0].IsStart","true"), new("States[0].IsAccepting","false"),
            new("States[1].Id","2"), new("States[1].IsStart","false"), new("States[1].IsAccepting","true"),
            new("Transitions[0].FromStateId","1"), new("Transitions[0].ToStateId","2"), new("Transitions[0].Symbol","a")
        };
        var resp = await client.PostAsync("/Automaton/ExportJson", new FormUrlEncodedContent(form));
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        var contentDisp = resp.Content.Headers.ContentDisposition;
        contentDisp!.FileName.ShouldNotBeNull();
        contentDisp.FileName.ShouldContain("automaton-");
    }

    [Fact]
    public async Task Home_ExportText_ReturnsFile()
    {
        var client = GetHttpClient();
        var form = new List<KeyValuePair<string,string>>
        {
            new("Type","NFA"),
            new("States[0].Id","1"), new("States[0].IsStart","true"), new("States[0].IsAccepting","false"),
            new("States[1].Id","2"), new("States[1].IsStart","false"), new("States[1].IsAccepting","true"),
            new("Transitions[0].FromStateId","1"), new("Transitions[0].ToStateId","2"), new("Transitions[0].Symbol","a"),
            new("Transitions[1].FromStateId","1"), new("Transitions[1].ToStateId","1"), new("Transitions[1].Symbol","a")
        };
        var resp = await client.PostAsync("/Automaton/ExportText", new FormUrlEncodedContent(form));
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.ShouldBe("text/plain");
        var body = await resp.Content.ReadAsStringAsync();
        body.ShouldContain("$states:");
        body.ShouldContain("$transitions:");
    }
}
