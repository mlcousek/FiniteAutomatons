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

    [Fact]
    public async Task GroupMembership_AddRemoveList_And_SavePermissions()
    {
        using var db = CreateInMemoryDb("group_membership_tests");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var ownerId = "owner-1";
        var memberId = "member-1";
        var outsiderId = "other-1";

        var group = await svc.CreateGroupAsync(ownerId, "TeamA", null);

        // initially no members
        var members0 = await svc.ListGroupMembersAsync(group.Id);
        members0.Count.ShouldBe(0);

        // add a member
        await svc.AddGroupMemberAsync(group.Id, memberId);
        var members1 = await svc.ListGroupMembersAsync(group.Id);
        members1.Count.ShouldBe(1);
        members1[0].UserId.ShouldBe(memberId);

        // add another member
        await svc.AddGroupMemberAsync(group.Id, outsiderId);
        var members2 = await svc.ListGroupMembersAsync(group.Id);
        members2.Count.ShouldBe(2);

        // remove one
        await svc.RemoveGroupMemberAsync(group.Id, outsiderId);
        var members3 = await svc.ListGroupMembersAsync(group.Id);
        members3.Count.ShouldBe(1);
        members3[0].UserId.ShouldBe(memberId);

        // By default MembersCanShare is true => non-member should not be able to save, member can
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = new List<State> { new() { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = new List<Transition>()
        };

        // outsider (not member) should fail
        await Should.ThrowAsync<UnauthorizedAccessException>(async () => await svc.SaveAsync(outsiderId, "o1", null, model, saveExecutionState: false, groupId: group.Id));

        // member should succeed
        var savedByMember = await svc.SaveAsync(memberId, "m1", null, model, saveExecutionState: false, groupId: group.Id);
        savedByMember.ShouldNotBeNull();
        savedByMember.UserId.ShouldBe(memberId);
        savedByMember.GroupId.ShouldBe(group.Id);
    }

    [Fact]
    public async Task MembersCanShare_False_AllowsOnlyOwnerToSave()
    {
        using var db = CreateInMemoryDb("members_canship_false");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var ownerId = "owner-2";
        var memberId = "member-2";

        var group = await svc.CreateGroupAsync(ownerId, "TeamB", null);

        // explicitly set MembersCanShare to false
        var grpEntity = db.SavedAutomatonGroups.First(g => g.Id == group.Id);
        grpEntity.MembersCanShare = false;
        await db.SaveChangesAsync();

        // add a member
        await svc.AddGroupMemberAsync(group.Id, memberId);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = new List<State> { new() { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = new List<Transition>()
        };

        // member should not be allowed to save when MembersCanShare == false
        await Should.ThrowAsync<UnauthorizedAccessException>(async () => await svc.SaveAsync(memberId, "m2", null, model, saveExecutionState: false, groupId: group.Id));

        // owner can save
        var savedByOwner = await svc.SaveAsync(ownerId, "ownerSave", null, model, saveExecutionState: false, groupId: group.Id);
        savedByOwner.ShouldNotBeNull();
        savedByOwner.UserId.ShouldBe(ownerId);
    }
}
