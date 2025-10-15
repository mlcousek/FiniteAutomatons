using System.Text.Json;
using FiniteAutomatons.Data;
using FiniteAutomatons.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Models.DoMain;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

public class SavedAutomatonServiceTests
{
    private ApplicationDbContext CreateInMemoryDb(string name)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: name)
            .Options;
        return new ApplicationDbContext(options);
    }

    [Fact]
    public async Task SaveAsync_SavesPayload_WithoutExecutionState()
    {
        using var db = CreateInMemoryDb("save_no_state");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = new List<State> { new() { Id = 1, IsStart = true, IsAccepting = false } },
            Transitions = new List<Transition> { new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' } }
        };

        var res = await svc.SaveAsync("user-1", "MyAut", "desc", model, saveExecutionState: false);

        res.Id.ShouldBeGreaterThan(0);
        res.UserId.ShouldBe("user-1");
        res.Name.ShouldBe("MyAut");
        res.HasExecutionState.ShouldBeFalse();
        res.ContentJson.ShouldNotBeNullOrWhiteSpace();

        // content should be valid JSON and contain type information
        var doc = JsonDocument.Parse(res.ContentJson);
        doc.RootElement.TryGetProperty("Type", out var typeProp).ShouldBeTrue();
    }

    [Fact]
    public async Task SaveAsync_SavesExecutionState_WhenRequested()
    {
        using var db = CreateInMemoryDb("save_with_state");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = new List<State> { new() { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = new List<Transition>(),
            Input = "abc",
            Position = 2,
            CurrentStateId = 1,
            CurrentStates = new HashSet<int> { 1 },
            IsAccepted = true,
            StateHistorySerialized = "history",
            StackSerialized = "stack"
        };

        var res = await svc.SaveAsync("user-2", "WithState", null, model, saveExecutionState: true);

        res.HasExecutionState.ShouldBeTrue();
        res.ExecutionStateJson.ShouldNotBeNullOrWhiteSpace();

        // Execution JSON should deserialize and contain fields we set
        var doc = JsonDocument.Parse(res.ExecutionStateJson!);
        doc.RootElement.GetProperty("Input").GetString().ShouldBe("abc");
        doc.RootElement.GetProperty("Position").GetInt32().ShouldBe(2);
        doc.RootElement.GetProperty("CurrentStateId").GetInt32().ShouldBe(1);
        doc.RootElement.TryGetProperty("CurrentStates", out var cs).ShouldBeTrue();
    }

    [Fact]
    public async Task CreateGroup_ListGroups_ListForUser_FilterByGroup_GetAndDelete()
    {
        using var db = CreateInMemoryDb("group_tests");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var g1 = await svc.CreateGroupAsync("u1", "BGroup", "bdesc");
        var g2 = await svc.CreateGroupAsync("u1", "AGroup", null);

        var groups = await svc.ListGroupsForUserAsync("u1");
        groups.Select(g => g.Name).ShouldBe(new[] { "AGroup", "BGroup" }); // ordered by name

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = new List<State> { new() { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = new List<Transition>()
        };

        var s1 = await svc.SaveAsync("u1", "inG1", null, model, saveExecutionState: false, groupId: g1.Id);
        var s2 = await svc.SaveAsync("u1", "inG2", null, model, saveExecutionState: false, groupId: g2.Id);
        var s3 = await svc.SaveAsync("u1", "nogroup", null, model, saveExecutionState: false, groupId: null);

        var allForUser = await svc.ListForUserAsync("u1");
        allForUser.Count.ShouldBe(3);

        var listG1 = await svc.ListForUserAsync("u1", groupId: g1.Id);
        listG1.Count.ShouldBe(1);
        listG1[0].Name.ShouldBe("inG1");

        var fetched = await svc.GetAsync(s1.Id, "u1");
        fetched.ShouldNotBeNull();
        fetched!.Name.ShouldBe("inG1");

        await svc.DeleteAsync(s1.Id, "u1");
        var afterDelete = await svc.GetAsync(s1.Id, "u1");
        afterDelete.ShouldBeNull();
    }

    // Additional tests: argument validation, empty states/transitions, list empty

    [Fact]
    public async Task SaveAsync_ThrowsOnNullArguments()
    {
        using var db = CreateInMemoryDb("arg_tests");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var model = new AutomatonViewModel { Type = AutomatonType.DFA };
        await Should.ThrowAsync<ArgumentNullException>(async () => await svc.SaveAsync(null!, "n", null, model));
        await Should.ThrowAsync<ArgumentNullException>(async () => await svc.SaveAsync("u", null!, null, model));
        // note: do not pass a null for model parameter here because signature is non-nullable in C# 11+ tests
    }

    [Fact]
    public async Task SaveAsync_NullStatesAndTransitions_AreSerializedAsEmptyCollections()
    {
        using var db = CreateInMemoryDb("empty_cols");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var model = new AutomatonViewModel { Type = AutomatonType.DFA, States = null!, Transitions = null! };
        var res = await svc.SaveAsync("u10", "e1", null, model, saveExecutionState: false);

        res.ContentJson.ShouldNotBeNullOrWhiteSpace();
        var doc = JsonDocument.Parse(res.ContentJson);
        doc.RootElement.TryGetProperty("States", out var states).ShouldBeTrue();
        states.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Array);
        states.GetArrayLength().ShouldBe(0);
        doc.RootElement.TryGetProperty("Transitions", out var trans).ShouldBeTrue();
        trans.ValueKind.ShouldBe(System.Text.Json.JsonValueKind.Array);
        trans.GetArrayLength().ShouldBe(0);
    }

    [Fact]
    public async Task ListForUserAsync_NoItems_ReturnsEmpty()
    {
        using var db = CreateInMemoryDb("empty_list");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var list = await svc.ListForUserAsync("nouser");
        list.ShouldNotBeNull();
        list.ShouldBeEmpty();
    }

    [Fact]
    public async Task CreateGroup_ManyGroups_Behavior()
    {
        using var db = CreateInMemoryDb("many_groups");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        for (int i = 0; i < 10; i++)
        {
            await svc.CreateGroupAsync("multi", $"Group{i}", null);
        }

        var groups = await svc.ListGroupsForUserAsync("multi");
        groups.Count.ShouldBe(10);
    }
}
