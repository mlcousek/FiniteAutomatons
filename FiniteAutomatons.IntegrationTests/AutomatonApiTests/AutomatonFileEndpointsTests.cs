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
    public async Task ImportAutomaton_TextFileFallback_Works()
    {
        var client = GetHttpClient();
        var txt = "$states:\nq0\nq1\n\n$initial:\nq0\n\n$accepting:\nq1\n\n$transitions:\nq0:?>q1\n";
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes(txt)), "upload", "enfa.txt" }
        };

        var response = await client.PostAsync("/ImportExport/ImportAutomaton", content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("AUTOMATON");
        // Epsilon transition should be visible in the rendered transitions list
        html.ShouldContain("ε");
    }

    [Fact]
    public async Task ImportAutomaton_FullViewModelJsonWithState_Works()
    {
        var client = GetHttpClient();
        var vm = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }],
            Input = "abba",
            Position = 2,
            HasExecuted = true,
            CurrentStateId = 2,
            StateHistorySerialized = "[]",
            IsCustomAutomaton = true
        };

        var json = System.Text.Json.JsonSerializer.Serialize(vm);
        using var content = new MultipartFormDataContent
        {
            { new ByteArrayContent(Encoding.UTF8.GetBytes(json)), "upload", "vm.json" }
        };

        var response = await client.PostAsync("/ImportExport/ImportAutomaton", content);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var html = await response.Content.ReadAsStringAsync();
        html.ShouldContain("AUTOMATON");
        // Execution state should be visible in the UI
        html.ShouldContain("Current Position");
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

    [Fact]
    public async Task ExportJsonWithState_ReturnsFile()
    {
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }, new() { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }],
            Input = "abba",
            Position = 1,
            HasExecuted = true,
            CurrentStateId = 1,
            StateHistorySerialized = "[]",
            IsCustomAutomaton = true
        };
        var form = new List<KeyValuePair<string, string>>
        {
            new("Type", model.Type.ToString()),
            new("Input", model.Input),
            new("HasExecuted", model.HasExecuted.ToString().ToLowerInvariant()),
            new("Position", model.Position.ToString()),
            new("CurrentStateId", model.CurrentStateId?.ToString() ?? ""),
            new("StateHistorySerialized", model.StateHistorySerialized),
            new("States[0].Id","1"), new("States[0].IsStart","true"), new("States[0].IsAccepting","false"),
            new("States[1].Id","2"), new("States[1].IsStart","false"), new("States[1].IsAccepting","true"),
            new("Transitions[0].FromStateId","1"), new("Transitions[0].ToStateId","2"), new("Transitions[0].Symbol","a")
        };
        var resp = await client.PostAsync("/ImportExport/ExportJsonWithState", new FormUrlEncodedContent(form));
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        resp.Content.Headers.ContentType!.MediaType.ShouldBe("application/json");
        var content = await resp.Content.ReadAsStringAsync();
        content.ShouldContain("\"Input\"");
        content.ShouldContain("abba");
    }
}
