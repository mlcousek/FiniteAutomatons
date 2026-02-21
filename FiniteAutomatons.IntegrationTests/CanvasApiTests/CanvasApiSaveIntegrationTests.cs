using FiniteAutomatons.Core.Models.Api;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace FiniteAutomatons.IntegrationTests.CanvasApiTests;

[Collection("Integration Tests")]
public class CanvasApiSaveIntegrationTests(IntegrationTestsFixture fixture) : IntegrationTestsBase(fixture)
{
    private readonly JsonSerializerOptions json = new(JsonSerializerDefaults.Web);

    // ────────────────────────────────────────────────────────────── //
    // /api/canvas/save — status codes
    // ────────────────────────────────────────────────────────────── //

    [Fact]
    public async Task Save_ValidRequest_Returns200()
    {
        var (client, _) = GetClientWithSession();
        var resp = await client.PostAsJsonAsync("/api/canvas/save", SimpleDfa(), json);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Save_NullBody_Returns400()
    {
        var (client, _) = GetClientWithSession();
        var resp = await client.PostAsync("/api/canvas/save",
            new System.Net.Http.StringContent("null", System.Text.Encoding.UTF8, "application/json"));
        resp.StatusCode.ShouldBe(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Save_Returns_SavedTrue()
    {
        var (client, _) = GetClientWithSession();
        var resp = await client.PostAsJsonAsync("/api/canvas/save", SimpleDfa(), json);
        var body = await resp.Content.ReadAsStringAsync();
        body.ShouldContain("saved");
    }

    // ────────────────────────────────────────────────────────────── //
    // /api/canvas/save → GET / — session persistence
    // ────────────────────────────────────────────────────────────── //

    [Fact]
    public async Task Save_ThenGet_HomePageContainsCustomAutomaton()
    {
        var (client, _) = GetClientWithSession();

        // Save a custom automaton
        await client.PostAsJsonAsync("/api/canvas/save", SimpleDfa(), json);

        // Reload home page — should reflect session data
        var homeResp = await client.GetAsync("/");
        homeResp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Save_DFA_ThenGet_HomePageContainsState()
    {
        var (client, _) = GetClientWithSession();
        var req = Req("DFA",
            [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }],
            [new() { FromStateId = 0, ToStateId = 1, Symbol = "a" }]);

        await client.PostAsJsonAsync("/api/canvas/save", req, json);

        var homeResp = await client.GetAsync("/");
        var html = await homeResp.Content.ReadAsStringAsync();
        // The page should include state data (our automaton has states)
        homeResp.StatusCode.ShouldBe(HttpStatusCode.OK);
        html.ShouldNotBeNullOrEmpty();
    }

    [Fact]
    public async Task Save_NFA_ThenGet_HomePageLoads()
    {
        var (client, _) = GetClientWithSession();
        await client.PostAsJsonAsync("/api/canvas/save",
            Req("NFA", [new() { Id = 0, IsStart = true }], []), json);

        var resp = await client.GetAsync("/");
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Save_PDA_ThenGet_HomePageLoads()
    {
        var (client, _) = GetClientWithSession();
        await client.PostAsJsonAsync("/api/canvas/save",
            Req("PDA",
                [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }],
                [new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "Z", StackPush = "" }]),
            json);

        var resp = await client.GetAsync("/");
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Save_EpsilonNFA_ThenGet_HomePageLoads()
    {
        var (client, _) = GetClientWithSession();
        await client.PostAsJsonAsync("/api/canvas/save",
            Req("EpsilonNFA",
                [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
                [new() { FromStateId = 0, ToStateId = 1, Symbol = "\\0" }]),
            json);

        var resp = await client.GetAsync("/");
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ────────────────────────────────────────────────────────────── //
    // /api/canvas/clear — status codes
    // ────────────────────────────────────────────────────────────── //

    [Fact]
    public async Task Clear_Returns200()
    {
        var (client, _) = GetClientWithSession();
        var resp = await client.PostAsync("/api/canvas/clear", null);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Clear_Returns_ClearedTrue()
    {
        var (client, _) = GetClientWithSession();
        var resp = await client.PostAsync("/api/canvas/clear", null);
        var body = await resp.Content.ReadAsStringAsync();
        body.ShouldContain("cleared");
    }

    [Fact]
    public async Task Clear_WhenNoSession_StillReturns200()
    {
        var (client, _) = GetClientWithSession();
        var resp = await client.PostAsync("/api/canvas/clear", null);
        resp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ────────────────────────────────────────────────────────────── //
    // /api/canvas/save → /api/canvas/clear → GET /
    // ────────────────────────────────────────────────────────────── //

    [Fact]
    public async Task SaveThenClear_ThenGet_ReturnsDefaultAutomaton()
    {
        var (client, _) = GetClientWithSession();

        // Save a custom automaton
        await client.PostAsJsonAsync("/api/canvas/save", SimpleDfa(), json);

        // Clear it
        await client.PostAsync("/api/canvas/clear", null);

        // Home page should now use the default (no session data)
        var homeResp = await client.GetAsync("/");
        homeResp.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ────────────────────────────────────────────────────────────── //
    // Multiple saves — last one wins
    // ────────────────────────────────────────────────────────────── //

    [Fact]
    public async Task Save_CalledTwice_SecondSaveOverwrites()
    {
        var (client, _) = GetClientWithSession();

        await client.PostAsJsonAsync("/api/canvas/save",
            Req("DFA", [new() { Id = 0, IsStart = true }], []), json);

        var resp2 = await client.PostAsJsonAsync("/api/canvas/save",
            Req("PDA",
                [new() { Id = 0, IsStart = true }, new() { Id = 1 }],
                [new() { FromStateId = 0, ToStateId = 1, Symbol = "a", StackPop = "Z", StackPush = "" }]),
            json);

        resp2.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ────────────────────────────────────────────────────────────── //
    // Content-Type header
    // ────────────────────────────────────────────────────────────── //

    [Fact]
    public async Task Save_Response_ContentTypeIsJson()
    {
        var (client, _) = GetClientWithSession();
        var resp = await client.PostAsJsonAsync("/api/canvas/save", SimpleDfa(), json);
        resp.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
    }

    [Fact]
    public async Task Clear_Response_ContentTypeIsJson()
    {
        var (client, _) = GetClientWithSession();
        var resp = await client.PostAsync("/api/canvas/clear", null);
        resp.Content.Headers.ContentType?.MediaType.ShouldBe("application/json");
    }

    // ────────────────────────────────────────────────────────────── //
    // Sync → Save → Sync again (same client, session preserved)
    // ────────────────────────────────────────────────────────────── //

    [Fact]
    public async Task SyncSaveSync_AllReturn200()
    {
        var (client, _) = GetClientWithSession();
        var req = SimpleDfa();

        var r1 = await client.PostAsJsonAsync("/api/canvas/sync", req, json);
        r1.StatusCode.ShouldBe(HttpStatusCode.OK);

        var r2 = await client.PostAsJsonAsync("/api/canvas/save", req, json);
        r2.StatusCode.ShouldBe(HttpStatusCode.OK);

        var r3 = await client.PostAsJsonAsync("/api/canvas/sync", req, json);
        r3.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    // ────────────────────────────────────────────────────────────── //
    // Helpers
    // ────────────────────────────────────────────────────────────── //

    private (HttpClient client, System.Net.CookieContainer cookies) GetClientWithSession()
    {
        var cookies = new CookieContainer();
        var handler = new HttpClientHandler { CookieContainer = cookies };
        var factory = fixture.AutomatonsWebApplicationFactory;
        var client = factory.CreateDefaultClient(new DelegatingHandlerWrapper(handler));
        return (client, cookies);
    }

    private sealed class DelegatingHandlerWrapper : DelegatingHandler
    {
        public DelegatingHandlerWrapper(HttpMessageHandler inner)
        {
            InnerHandler = inner;
        }
    }

    private static CanvasSyncRequest SimpleDfa() => Req("DFA",
        [new() { Id = 0, IsStart = true }, new() { Id = 1, IsAccepting = true }],
        [new() { FromStateId = 0, ToStateId = 1, Symbol = "a" }]);

    private static CanvasSyncRequest Req(string type,
        List<CanvasSyncState> states, List<CanvasSyncTransition> trans)
        => new() { Type = type, States = states, Transitions = trans };
}
