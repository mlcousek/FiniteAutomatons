using FiniteAutomatons.Core.Models.Api;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace FiniteAutomatons.IntegrationTests.CanvasApiTests;

[Collection("Integration Tests")]
public class CanvasApiSyncIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);

    [Fact]
    public async Task Sync_ValidRequest_Returns200()
    {
        var resp = await PostSync(SimpleDfa());
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Sync_NullBody_Returns400()
    {
        var client = GetHttpClient();
        var content = new StringContent("null", Encoding.UTF8, "application/json");
        var resp = await client.PostAsync("/api/canvas/sync", content);
        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Sync_InvalidJsonBody_Returns400Or500()
    {
        var client = GetHttpClient();
        var content = new StringContent("not json", Encoding.UTF8, "application/json");
        var resp = await client.PostAsync("/api/canvas/sync", content);
        ((int)resp.StatusCode).ShouldBeGreaterThanOrEqualTo(400);
    }

    [Fact]
    public async Task Sync_EmptyRequest_Returns200WithEmptyData()
    {
        var resp = await PostSync(new CanvasSyncRequest { Type = "DFA", States = [], Transitions = [] });
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
        var body = await ReadResponse(resp);
        body.Alphabet.ShouldBeEmpty();
        body.States.ShouldBeEmpty();
        body.Transitions.ShouldBeEmpty();
    }

    [Fact]
    public async Task Sync_DFA_SingleTransition_AlphabetHasOneSymbol()
    {
        var req = Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "a" }]);

        var body = await ReadResponse(await PostSync(req));
        body.Alphabet.ShouldContain("a");
        body.Alphabet.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Sync_DFA_MultipleDistinctSymbols_AllInAlphabet()
    {
        var req = Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            [
                new() { FromStateId = 0, ToStateId = 1, Symbol = "a" },
                new() { FromStateId = 1, ToStateId = 0, Symbol = "b" }
            ]);

        var body = await ReadResponse(await PostSync(req));
        body.Alphabet.ShouldContain("a");
        body.Alphabet.ShouldContain("b");
    }

    [Fact]
    public async Task Sync_DuplicateSymbols_AlphabetDistinct()
    {
        var req = Req("NFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }, new() { Id = 2 }],
            [
                new() { FromStateId = 0, ToStateId = 1, Symbol = "a" },
                new() { FromStateId = 0, ToStateId = 2, Symbol = "a" }
            ]);

        var body = await ReadResponse(await PostSync(req));
        body.Alphabet.Count.ShouldBe(1);
    }

    [Fact]
    public async Task Sync_EpsilonTransition_NotInAlphabet_HasEpsilonTrue()
    {
        var req = Req("EpsilonNFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "\\0" }]);

        var body = await ReadResponse(await PostSync(req));
        body.Alphabet.ShouldBeEmpty();
        body.HasEpsilonTransitions.ShouldBeTrue();
    }

    [Fact]
    public async Task Sync_AlphabetIsSorted()
    {
        var req = Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            [
                new() { FromStateId = 0, ToStateId = 1, Symbol = "c" },
                new() { FromStateId = 1, ToStateId = 0, Symbol = "a" },
                new() { FromStateId = 0, ToStateId = 0, Symbol = "b" }
            ]);

        var body = await ReadResponse(await PostSync(req));
        body.Alphabet.ShouldBe([.. body.Alphabet.OrderBy(x => x)]);
    }

    [Fact]
    public async Task Sync_States_CountMatchesInput()
    {
        var req = Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }, new() { Id = 2, IsAccepting = true }],
            []);

        var body = await ReadResponse(await PostSync(req));
        body.StateCount.ShouldBe(3);
        body.States.Count.ShouldBe(3);
    }

    [Fact]
    public async Task Sync_States_SortedById()
    {
        var req = Req("DFA",
            [new() { Id = 5 }, new() { Id = 2 }, new() { Id = 0, IsStart = true }],
            []);

        var body = await ReadResponse(await PostSync(req));
        body.States.Select(s => s.Id).ShouldBe([0, 2, 5]);
    }

    [Fact]
    public async Task Sync_States_IsStartFlagPreserved()
    {
        var req = Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            []);

        var body = await ReadResponse(await PostSync(req));
        body.States.Single(s => s.Id == 0).IsStart.ShouldBeTrue();
        body.States.Single(s => s.Id == 1).IsStart.ShouldBeFalse();
    }

    [Fact]
    public async Task Sync_States_IsAcceptingFlagPreserved()
    {
        var req = Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }],
            []);

        var body = await ReadResponse(await PostSync(req));
        body.States.Single(s => s.Id == 1).IsAccepting.ShouldBeTrue();
    }

    [Fact]
    public async Task Sync_States_LabelDerivedFromId()
    {
        var req = Req("DFA", [new() { Id = 7, IsStart = true }], []);
        var body = await ReadResponse(await PostSync(req));
        body.States[0].Label.ShouldBe("q7");
    }

    [Fact]
    public async Task Sync_Transitions_CountMatchesInput()
    {
        var req = Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }, new() { Id = 2 }],
            [
                new() { FromStateId = 0, ToStateId = 1, Symbol = "a" },
                new() { FromStateId = 1, ToStateId = 2, Symbol = "b" }
            ]);

        var body = await ReadResponse(await PostSync(req));
        body.TransitionCount.ShouldBe(2);
        body.Transitions.Count.ShouldBe(2);
    }

    [Fact]
    public async Task Sync_Transitions_SymbolDisplayCorrect()
    {
        var req = Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "x" }]);

        var body = await ReadResponse(await PostSync(req));
        body.Transitions[0].SymbolDisplay.ShouldBe("x");
    }

    [Fact]
    public async Task Sync_Transitions_EpsilonDisplayedAsEpsilonChar()
    {
        var req = Req("EpsilonNFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "\\0" }]);

        var body = await ReadResponse(await PostSync(req));
        body.Transitions[0].SymbolDisplay.ShouldBe("ε");
    }

    [Fact]
    public async Task Sync_Transitions_SortedByFromStateId()
    {
        var req = Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }, new() { Id = 2 }],
            [
                new() { FromStateId = 2, ToStateId = 0, Symbol = "b" },
                new() { FromStateId = 0, ToStateId = 1, Symbol = "a" }
            ]);

        var body = await ReadResponse(await PostSync(req));
        body.Transitions[0].FromStateId.ShouldBe(0);
    }

    [Fact]
    public async Task Sync_SelfLoop_RecordedCorrectly()
    {
        var req = Req("DFA",
            [new() { Id = 0, IsStart = true, IsAccepting = true }],
            [new() { FromStateId = 0, ToStateId = 0, Symbol = "a" }]);

        var body = await ReadResponse(await PostSync(req));
        body.Transitions[0].FromStateId.ShouldBe(0);
        body.Transitions[0].ToStateId.ShouldBe(0);
    }

    [Theory]
    [InlineData("DFA", false)]
    [InlineData("NFA", false)]
    [InlineData("EpsilonNFA", false)]
    [InlineData("PDA", true)]
    public async Task Sync_IsPDA_CorrectForType(string type, bool expectedIsPDA)
    {
        var req = Req(type, [new() { Id = 0, IsStart = true }], []);
        var body = await ReadResponse(await PostSync(req));
        body.IsPDA.ShouldBe(expectedIsPDA);
    }

    [Fact]
    public async Task Sync_PDA_StackPopDisplayed()
    {
        var req = Req("PDA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "Z", StackPush = "AZ" }]);

        var body = await ReadResponse(await PostSync(req));
        body.Transitions[0].StackPopDisplay.ShouldBe("Z");
        body.Transitions[0].StackPush.ShouldBe("AZ");
    }

    [Fact]
    public async Task Sync_PDA_EpsilonStackPopDisplayedAsEpsilonChar()
    {
        var req = Req("PDA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "\\0", StackPush = "Z" }]);

        var body = await ReadResponse(await PostSync(req));
        body.Transitions[0].StackPopDisplay.ShouldBe("ε");
    }

    [Fact]
    public async Task Sync_NonPDA_StackPopNull()
    {
        var req = Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "a" }]);

        var body = await ReadResponse(await PostSync(req));
        body.Transitions[0].StackPopDisplay.ShouldBeNull();
    }

    [Fact]
    public async Task Sync_LargeAutomaton_AllDataReturned()
    {
        var states = Enumerable.Range(0, 15)
            .Select(i => new CanvasSyncState { Id = i, IsStart = i == 0, IsAccepting = i == 14 })
            .ToList();
        var transitions = Enumerable.Range(0, 14)
            .Select(i => new CanvasSyncTransition { FromStateId = i, ToStateId = i + 1, Symbol = "a" })
            .ToList();
        var req = new CanvasSyncRequest { Type = "DFA", States = states, Transitions = transitions };

        var body = await ReadResponse(await PostSync(req));
        body.StateCount.ShouldBe(15);
        body.TransitionCount.ShouldBe(14);
    }

    [Fact]
    public async Task Sync_Response_ContentTypeIsJson()
    {
        var resp = await PostSync(SimpleDfa());
        resp.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
    }

    // Helpers

    private async Task<HttpResponseMessage> PostSync(CanvasSyncRequest req)
    {
        var client = GetHttpClient();
        return await client.PostAsJsonAsync("/api/canvas/sync", req, json);
    }

    private async Task<CanvasSyncResponse> ReadResponse(HttpResponseMessage resp)
    {
        var json = await resp.Content.ReadAsStringAsync();
        return JsonSerializer.Deserialize<CanvasSyncResponse>(json, this.json)!;
    }

    private static CanvasSyncRequest SimpleDfa() => Req("DFA",
        [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }],
        [new() { FromStateId = 0, ToStateId = 1, Symbol = "a" }]);

    private static CanvasSyncRequest Req(string type,
        List<CanvasSyncState> states, List<CanvasSyncTransition> transitions)
        => new() { Type = type, States = states, Transitions = transitions };
}
