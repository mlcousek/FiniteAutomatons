using FiniteAutomatons.Core.Models.ViewModel;
using Shouldly;
using System.Net;

namespace FiniteAutomatons.IntegrationTests.AutomatonOperations.AutomatonMinimalization;

[Collection("Integration Tests")]
public class AutomatonMinimalizationButtonTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
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

    private static async Task<HttpResponseMessage> PostMinimalize(HttpClient c, AutomatonViewModel m) =>
        await c.PostAsync("/AutomatonExecution/Minimalize", new FormUrlEncodedContent(BuildForm(m)));

    [Fact]
    public async Task MinimalizeButton_WithReducibleDFA_MinimizesSuccessfully()
    {
        // Arrange 
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true },
                new() { Id = 3, IsStart = false, IsAccepting = false }, // Unreachable
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
            ],
            Input = "a",
            IsCustomAutomaton = true
        };

        // Act 
        var response = await PostMinimalize(client, model);

        // Assert
        (response.StatusCode == HttpStatusCode.Redirect || response.StatusCode == HttpStatusCode.OK).ShouldBeTrue();

        var html = response.StatusCode == HttpStatusCode.Redirect
            ? await (await client.GetAsync(response.Headers.Location)).Content.ReadAsStringAsync()
            : await response.Content.ReadAsStringAsync();

        html.ShouldContain("DFA minimized:");
        html.ShouldContain("3"); // Original count
        html.ShouldContain("2 states"); // Minimized count
    }

    [Fact]
    public async Task MinimalizeButton_WithMinimalDFA_ShowsAlreadyMinimalMessage()
    {
        // Arrange 
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true },
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 1, Symbol = 'a' },
            ],
            IsCustomAutomaton = true
        };

        // Act
        var response = await PostMinimalize(client, model);

        // Assert
        var html = response.StatusCode == HttpStatusCode.Redirect
            ? await (await client.GetAsync(response.Headers.Location)).Content.ReadAsStringAsync()
            : await response.Content.ReadAsStringAsync();

        html.ShouldContain("already minimal");
        html.ShouldContain("2 states");
    }

    [Fact]
    public async Task MinimalizeButton_WithNonDFA_ShowsErrorMessage()
    {
        // Arrange 
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            IsCustomAutomaton = true
        };

        // Act
        var response = await PostMinimalize(client, model);

        // Assert
        var html = response.StatusCode == HttpStatusCode.Redirect
            ? await (await client.GetAsync(response.Headers.Location)).Content.ReadAsStringAsync()
            : await response.Content.ReadAsStringAsync();

        html.ShouldContain("Minimization supported only for DFA");
    }

    [Fact]
    public async Task MinimalizeButton_AfterExecutionStarts_ShowsErrorMessage()
    {
        // Arrange 
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true },
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
            ],
            Input = "a",
            HasExecuted = true, // Simulation started
            Position = 1,
            IsCustomAutomaton = true
        };

        // Act 
        var response = await PostMinimalize(client, model);

        // Assert
        var html = response.StatusCode == HttpStatusCode.Redirect
            ? await (await client.GetAsync(response.Headers.Location)).Content.ReadAsStringAsync()
            : await response.Content.ReadAsStringAsync();

        html.ShouldContain("Cannot minimalize after execution has started");
    }

    [Fact]
    public async Task MinimalizeButton_WithEquivalentStates_MergesThem()
    {
        // Arrange
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true },
                new() { Id = 3, IsStart = false, IsAccepting = true },
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 1, ToStateId = 3, Symbol = 'b' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'b' },
                new() { FromStateId = 3, ToStateId = 3, Symbol = 'a' },
                new() { FromStateId = 3, ToStateId = 3, Symbol = 'b' },
            ],
            IsCustomAutomaton = true
        };

        // Act
        var response = await PostMinimalize(client, model);

        // Assert
        var html = response.StatusCode == HttpStatusCode.Redirect
            ? await (await client.GetAsync(response.Headers.Location)).Content.ReadAsStringAsync()
            : await response.Content.ReadAsStringAsync();

        // Should have merged states 2 and 3
        html.ShouldContain("3");
        html.ShouldContain("2 states");
        html.ShouldContain("New state");
    }

    [Fact]
    public async Task MinimalizeButton_PreservesInputString()
    {
        // Arrange
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true },
                new() { Id = 3, IsStart = false, IsAccepting = false }, // Unreachable
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
            ],
            Input = "test input string",
            IsCustomAutomaton = true
        };

        // Act
        var response = await PostMinimalize(client, model);

        // Assert
        var html = response.StatusCode == HttpStatusCode.Redirect
            ? await (await client.GetAsync(response.Headers.Location)).Content.ReadAsStringAsync()
            : await response.Content.ReadAsStringAsync();

        html.ShouldContain("test input string");
    }

    [Fact]
    public async Task MinimalizeButton_WithMultipleUnreachableStates_RemovesAll()
    {
        // Arrange 
        var client = GetHttpClient();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true },
                new() { Id = 3, IsStart = false, IsAccepting = false }, // Unreachable
                new() { Id = 4, IsStart = false, IsAccepting = false }, // Unreachable
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
            ],
            IsCustomAutomaton = true
        };

        // Act
        var response = await PostMinimalize(client, model);

        // Assert
        var html = response.StatusCode == HttpStatusCode.Redirect
            ? await (await client.GetAsync(response.Headers.Location)).Content.ReadAsStringAsync()
            : await response.Content.ReadAsStringAsync();

        html.ShouldContain("4");
        html.ShouldContain("2 states");
    }
}
