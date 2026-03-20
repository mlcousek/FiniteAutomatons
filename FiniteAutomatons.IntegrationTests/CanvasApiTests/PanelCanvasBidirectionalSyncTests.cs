using FiniteAutomatons.Core.Models.Api;
using Shouldly;
using System.Net.Http.Json;
using System.Text.Json;

namespace FiniteAutomatons.IntegrationTests.CanvasApiTests;

[Collection("Integration Tests")]
public class PanelCanvasBidirectionalSyncTests(IntegrationTestsFixture fixture)
    : IntegrationTestsBase(fixture)
{
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);

    private async Task<HttpResponseMessage> PostSync(CanvasSyncRequest req)
    {
        var client = GetHttpClient();
        return await client.PostAsJsonAsync("/api/canvas/sync", req, json);
    }

    private async Task<CanvasSyncResponse> ReadBody(HttpResponseMessage resp)
    {
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<CanvasSyncResponse>(json, this.json)!;
    }

    private static CanvasSyncRequest Req(string type,
        List<CanvasSyncState> states, List<CanvasSyncTransition> transitions)
        => new() { Type = type, States = states, Transitions = transitions };

    // ─────────────────────────────────────────────────────
    // Panel → Canvas: State management
    // ─────────────────────────────────────────────────────

    [Fact]
    public async Task PanelStateAdded_SyncReturns_NewStateInList()
    {
        var req = Req("DFA",
            [
                new() { Id = 0, IsStart = true },
                new() { Id = 1, IsAccepting = true },
                // "panel added" this new state:
                new() { Id = 2, IsStart = false, IsAccepting = false }
            ],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "a" }]);

        var body = await ReadBody(await PostSync(req));

        body.StateCount.ShouldBe(3, "new state should be counted");
        body.States.Single(s => s.Id == 2).IsStart.ShouldBeFalse();
        body.States.Single(s => s.Id == 2).IsAccepting.ShouldBeFalse();
    }

    [Fact]
    public async Task PanelStateDeleted_SyncReturns_FewerStates()
    {

        var req = Req("DFA",
            [
                new() { Id = 0, IsStart = true },
                // state 1 was "deleted" via panel — not included in sync
            ],
            []);

        var body = await ReadBody(await PostSync(req));

        body.StateCount.ShouldBe(1, "deleted state should not appear");
        body.States.ShouldNotContain(s => s.Id == 1);
    }

    [Fact]
    public async Task PanelToggleStart_SyncReturns_UpdatedStartFlag()
    {
        var req = Req("DFA",
            [
                new() { Id = 0, IsStart = false },   // was start, toggled off via panel
                new() { Id = 1, IsStart = true }     // toggled to start via panel
            ],
            []);

        var body = await ReadBody(await PostSync(req));

        body.States.Single(s => s.Id == 0).IsStart.ShouldBeFalse();
        body.States.Single(s => s.Id == 1).IsStart.ShouldBeTrue();
    }

    [Fact]
    public async Task PanelToggleAccepting_SyncReturns_UpdatedAcceptingFlag()
    {
        var req = Req("DFA",
            [
                new() { Id = 0, IsStart = true, IsAccepting = false },
                new() { Id = 1, IsAccepting = true }  // panel toggled accepting ON
            ],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "b" }]);

        var body = await ReadBody(await PostSync(req));

        body.States.Single(s => s.Id == 1).IsAccepting.ShouldBeTrue();
    }

    [Fact]
    public async Task PanelTransitionAdded_SyncReturns_NonEmptyAlphabet()
    {
        var req = Req("NFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "x" }]);

        var body = await ReadBody(await PostSync(req));

        body.Alphabet.ShouldContain("x");
        body.TransitionCount.ShouldBe(1);
        body.Transitions[0].SymbolDisplay.ShouldBe("x");
    }

    [Fact]
    public async Task PanelTransitionDeleted_SyncReturns_EmptyAlphabet()
    {
        var req = Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }],
            []);

        var body = await ReadBody(await PostSync(req));

        body.Alphabet.ShouldBeEmpty("alphabet should be empty when no transitions remain");
        body.TransitionCount.ShouldBe(0);
    }

    [Fact]
    public async Task PanelTransitionAdded_EpsilonSymbol_SyncReturnsEpsilonInDisplay()
    {
        var req = Req("EpsilonNFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "\\0" }]);

        var body = await ReadBody(await PostSync(req));

        body.Transitions[0].SymbolDisplay.ShouldBe("ε");
        body.HasEpsilonTransitions.ShouldBeTrue();
    }

    [Fact]
    public async Task PanelPdaTransitionAdded_SyncReturns_StackOpDisplay()
    {
        var req = Req("PDA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "Z", StackPush = "AZ" }]);

        var body = await ReadBody(await PostSync(req));

        body.IsPDA.ShouldBeTrue();
        body.Transitions[0].StackPopDisplay.ShouldBe("Z");
        body.Transitions[0].StackPush.ShouldBe("AZ");
        body.Transitions[0].SymbolDisplay.ShouldBe("a");
    }

    [Fact]
    public async Task PanelPdaTransitionAdded_EpsilonStackPop_SyncReturnsEpsilonDisplay()
    {
        var req = Req("PDA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "(", StackPop = "\\0", StackPush = "(" }]);

        var body = await ReadBody(await PostSync(req));

        body.Transitions[0].StackPopDisplay.ShouldBe("ε", "epsilon stackPop should display as ε");
    }

    [Fact]
    public async Task PanelPdaStateAdded_SyncReturns_CorrectStateCount()
    {
        var req = Req("PDA",
            [
                new() { Id = 0, IsStart = true },
                new() { Id = 1, IsAccepting = true },
                new() { Id = 2 }  // added via panel
            ],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "Z", StackPush = "Z" }]);

        var body = await ReadBody(await PostSync(req));

        body.StateCount.ShouldBe(3);
        body.IsPDA.ShouldBeTrue();
    }

    [Fact]
    public async Task CanvasEdited_ThenPanelAddsState_SyncReflectsBothChanges()
    {
        // 1st sync: canvas adds state 2 (canvas edit)
        var afterCanvasEdit = Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }, new() { Id = 2 }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "a" }]);

        var bodyAfterCanvas = await ReadBody(await PostSync(afterCanvasEdit));
        bodyAfterCanvas.StateCount.ShouldBe(3);

        // 2nd sync: panel adds a transition from 0 to 2 via panel
        var afterPanelEdit = Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }, new() { Id = 2 }],
            [
                new() { FromStateId = 0, ToStateId = 1, Symbol = "a" },
                new() { FromStateId = 0, ToStateId = 2, Symbol = "b" }  // panel added this
            ]);

        var bodyAfterPanel = await ReadBody(await PostSync(afterPanelEdit));
        bodyAfterPanel.StateCount.ShouldBe(3);
        bodyAfterPanel.TransitionCount.ShouldBe(2);
        bodyAfterPanel.Alphabet.ShouldContain("a");
        bodyAfterPanel.Alphabet.ShouldContain("b");
    }

    [Fact]
    public async Task PanelEdited_ThenCanvasDeletesTransition_SyncReflectsDeletion()
    {
        // Panel adds a state and transition; then canvas removes the transition.
        var afterCanvasDelete = Req("NFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }, new() { Id = 2, IsAccepting = true }],
            // transition q0->q2 was deleted on canvas
            [new() { FromStateId = 1, ToStateId = 2, Symbol = "b" }]);

        var body = await ReadBody(await PostSync(afterCanvasDelete));

        body.Transitions.ShouldNotContain(t => t.FromStateId == 0 && t.ToStateId == 2);
        body.TransitionCount.ShouldBe(1);
    }

    [Fact]
    public async Task LargeAutomaton_PanelAddsMultipleStates_AllReturned()
    {
        // Simulate panel adding 5 new states to an existing 5-state DFA.
        var states = Enumerable.Range(0, 10)
            .Select(i => new CanvasSyncState { Id = i, IsStart = i == 0, IsAccepting = i == 9 })
            .ToList();

        var transitions = Enumerable.Range(0, 9)
            .Select(i => new CanvasSyncTransition { FromStateId = i, ToStateId = i + 1, Symbol = "a" })
            .ToList();

        var req = Req("DFA", states, transitions);
        var body = await ReadBody(await PostSync(req));

        body.StateCount.ShouldBe(10);
        body.TransitionCount.ShouldBe(9);
    }

    [Fact]
    public async Task PanelDeleteAllTransitions_SyncReturnsEmptyAlphabetAndTransitions()
    {
        var req = Req("EpsilonNFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }],
            []);  // all transitions deleted via panel

        var body = await ReadBody(await PostSync(req));

        body.TransitionCount.ShouldBe(0);
        body.Alphabet.ShouldBeEmpty();
        body.HasEpsilonTransitions.ShouldBeFalse();
    }

    [Fact]
    public async Task PanelAndCanvas_MultiSymbolTransition_AlphabetContainsBothSymbols()
    {
        var req = Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }],
            [
                new() { FromStateId = 0, ToStateId = 1, Symbol = "a" },
                new() { FromStateId = 0, ToStateId = 1, Symbol = "c" }
            ]);

        var body = await ReadBody(await PostSync(req));

        body.Alphabet.ShouldContain("a");
        body.Alphabet.ShouldContain("c");
        body.TransitionCount.ShouldBe(2);
    }

    [Fact]
    public async Task Panel_SelfLoop_AppearsInTransitions()
    {
        // Panel adds a self-loop on q0.
        var req = Req("DFA",
            [new() { Id = 0, IsStart = true, IsAccepting = true }],
            [new() { FromStateId = 0, ToStateId = 0, Symbol = "a" }]);

        var body = await ReadBody(await PostSync(req));

        var selfLoop = body.Transitions.Single(t => t.FromStateId == 0 && t.ToStateId == 0);
        selfLoop.SymbolDisplay.ShouldBe("a");
    }

    [Fact]
    public async Task SyncAfterPanelEdit_ContentTypeIsJson()
    {
        var req = Req("DFA",
            [new() { Id = 0, IsStart = true }], []);

        var resp = await PostSync(req);
        resp.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
    }
}
