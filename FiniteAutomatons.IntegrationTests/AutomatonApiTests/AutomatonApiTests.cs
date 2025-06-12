using FiniteAutomatons.Core.Models.DoMain;
using System.Net.Http.Json;

namespace FiniteAutomatons.IntegrationTests.AutomatonApiTests;

public class AutomatonApiTests(AutomatonsWebApplicationFactory<Program> factory) : IClassFixture<AutomatonsWebApplicationFactory<Program>>
{
    private HttpClient Client => factory.CreateClient();

    [Fact]
    public async Task SimulateDfa_AcceptsValidInput()
    {
        var dto = new
        {
            States = new List<State> {
                new() { Id = 0, IsStart = true, IsAccepting = false },
                new() { Id = 1, IsStart = false, IsAccepting = true }
            },
            Transitions = new List<Transition> {
                new() { FromStateId = 0, ToStateId = 1, Symbol = 'a' }
            },
            Input = "a"
        };
        var response = await Client.PostAsJsonAsync("/api/automaton/simulate-dfa", dto);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SimResult>();
        Assert.True(result != null && result.Accepted);
    }

    private class SimResult { public bool Accepted { get; set; } }
}
