using FiniteAutomatons.Core.Models.Api;
using Shouldly;
using System.Diagnostics;
using System.Net.Http.Json;

namespace FiniteAutomatons.IntegrationTests.AspireTests;

public class PerformanceAndResilienceTests(AspireTestFixture fixture) : IClassFixture<AspireTestFixture>
{
    private readonly AspireTestFixture fixture = fixture;

    [Fact]
    public async Task ApplicationStartsWithinAcceptableTime()
    {
        var stopwatch = Stopwatch.StartNew();

        var response = await fixture.HttpClient!.GetAsync("/health");

        stopwatch.Stop();

        response.IsSuccessStatusCode.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(5000);
    }

    [Fact]
    public async Task CanHandleMultipleConcurrentRequests()
    {
        var tasks = new List<Task<HttpResponseMessage>>();

        for (int i = 0; i < 10; i++)
        {
            tasks.Add(fixture.HttpClient!.GetAsync("/"));
        }

        var responses = await Task.WhenAll(tasks);

        responses.All(r => r.IsSuccessStatusCode).ShouldBeTrue();
    }

    [Fact]
    public async Task CanHandleConcurrentAutomatonOperations()
    {
        var syncRequest = new CanvasSyncRequest
        {
            Type = "DFA",
            States =
            [
                new CanvasSyncState { Id = 0, IsStart = true, IsAccepting = false },
                new CanvasSyncState { Id = 1, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new CanvasSyncTransition { FromStateId = 0, ToStateId = 1, Symbol = "a" }
            ]
        };

        var tasks = new List<Task<HttpResponseMessage>>();

        for (int i = 0; i < 5; i++)
        {
            tasks.Add(fixture.HttpClient!.PostAsJsonAsync("/api/canvas/sync", syncRequest));
        }

        var responses = await Task.WhenAll(tasks);

        responses.All(r => r.IsSuccessStatusCode).ShouldBeTrue();
    }

    [Fact]
    public async Task LargeAutomatonProcessingCompletes()
    {
        var states = new List<CanvasSyncState>();
        var transitions = new List<CanvasSyncTransition>();

        for (int i = 0; i < 50; i++)
        {
            states.Add(new CanvasSyncState
            {
                Id = i,
                IsStart = i == 0,
                IsAccepting = i == 49
            });

            if (i < 49)
            {
                transitions.Add(new CanvasSyncTransition
                {
                    FromStateId = i,
                    ToStateId = i + 1,
                    Symbol = "a"
                });
            }
        }

        var syncRequest = new CanvasSyncRequest
        {
            Type = "NFA",
            States = states,
            Transitions = transitions
        };

        var stopwatch = Stopwatch.StartNew();
        var response = await fixture.HttpClient!.PostAsJsonAsync("/api/canvas/sync", syncRequest);
        stopwatch.Stop();

        response.IsSuccessStatusCode.ShouldBeTrue();
        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(10000);
    }

    [Fact]
    public async Task ApplicationRecoverAfterInvalidRequests()
    {
        var invalidRequest = new CanvasSyncRequest
        {
            Type = "INVALID_TYPE",
            States = [],
            Transitions = []
        };

        await fixture.HttpClient!.PostAsJsonAsync("/api/canvas/sync", invalidRequest);

        var validRequest = new CanvasSyncRequest
        {
            Type = "DFA",
            States =
            [
                new CanvasSyncState { Id = 0, IsStart = true, IsAccepting = true }
            ],
            Transitions = []
        };

        var response2 = await fixture.HttpClient!.PostAsJsonAsync("/api/canvas/sync", validRequest);

        response2.IsSuccessStatusCode.ShouldBeTrue();
    }

    [Fact]
    public async Task HttpResilienceHandlerIsActive()
    {
        var tasks = new List<Task>();

        for (int i = 0; i < 20; i++)
        {
            tasks.Add(Task.Run(async () =>
            {
                await fixture.HttpClient!.GetAsync("/");
            }));
        }

        await Task.WhenAll(tasks);
    }

    [Fact]
    public async Task ComplexWorkflowPerformance()
    {
        var stopwatch = Stopwatch.StartNew();

        var syncRequest = new CanvasSyncRequest
        {
            Type = "DFA",
            States =
            [
                new CanvasSyncState { Id = 0, IsStart = true, IsAccepting = false },
                new CanvasSyncState { Id = 1, IsStart = false, IsAccepting = false },
                new CanvasSyncState { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new CanvasSyncTransition { FromStateId = 0, ToStateId = 1, Symbol = "a" },
                new CanvasSyncTransition { FromStateId = 1, ToStateId = 2, Symbol = "b" }
            ]
        };

        await fixture.HttpClient!.PostAsJsonAsync("/api/canvas/sync", syncRequest);
        await fixture.HttpClient!.GetAsync("/");
        await fixture.HttpClient!.PostAsync("/api/conversion/to-nfa", null);
        await fixture.HttpClient!.GetAsync("/api/export/json");

        stopwatch.Stop();

        stopwatch.ElapsedMilliseconds.ShouldBeLessThan(15000);
    }
}
