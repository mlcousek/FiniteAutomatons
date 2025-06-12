using System.Net.Http;
using System.Net.Http.Json;
using System.Threading.Tasks;
using Xunit;
using FiniteAutomatons.Core.Models.DoMain;
using System.Collections.Generic;

namespace FiniteAutomatons.IntegrationTests;

public class AutomatonApiTests : IClassFixture<AutomatonsWebApplicationFactory<Program>>
{
    private readonly AutomatonsWebApplicationFactory<Program> _factory;
    private HttpClient Client => _factory.CreateClient();

    public AutomatonApiTests(AutomatonsWebApplicationFactory<Program> factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task SimulateDfa_AcceptsValidInput()
    {
        var dto = new
        {
            States = new List<State> {
                new State { Id = 0, IsStart = true, IsAccepting = false },
                new State { Id = 1, IsStart = false, IsAccepting = true }
            },
            Transitions = new List<Transition> {
                new Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a' }
            },
            Input = "a"
        };
        var response = await Client.PostAsJsonAsync("/api/automaton/simulate-dfa", dto);
        response.EnsureSuccessStatusCode();
        var result = await response.Content.ReadFromJsonAsync<SimResult>();
        Assert.True(result != null && result.accepted);
    }

    private class SimResult { public bool accepted { get; set; } }
}
