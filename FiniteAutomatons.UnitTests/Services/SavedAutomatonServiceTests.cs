using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Data;
using FiniteAutomatons.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.Text.Json;

namespace FiniteAutomatons.UnitTests.Services;

public class SavedAutomatonServiceTests
{
    private static ApplicationDbContext CreateInMemoryDb(string name)
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
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' }]
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
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            Input = "abc",
            Position = 2,
            CurrentStateId = 1,
            CurrentStates = [1],
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
        groups.Select(g => g.Name).ShouldBe(["AGroup", "BGroup"]); // ordered by name

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
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
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        // outsider (not member) should fail
        await Should.ThrowAsync<UnauthorizedAccessException>(async () => await svc.SaveAsync(outsiderId, "o1", null, model, saveExecutionState: false, groupId: group.Id));

        // member should succeed
        var savedByMember = await svc.SaveAsync(memberId, "m1", null, model, saveExecutionState: false, groupId: group.Id);
        savedByMember.ShouldNotBeNull();
        savedByMember.UserId.ShouldBe(memberId);

        // Verify it was assigned to the group via the Assignments table
        var assignments = await db.SavedAutomatonGroupAssignments
            .Where(a => a.AutomatonId == savedByMember.Id)
            .ToListAsync();
        assignments.Count.ShouldBe(1);
        assignments[0].GroupId.ShouldBe(group.Id);
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
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        // member should not be allowed to save when MembersCanShare == false
        await Should.ThrowAsync<UnauthorizedAccessException>(async () => await svc.SaveAsync(memberId, "m2", null, model, saveExecutionState: false, groupId: group.Id));

        // owner can save
        var savedByOwner = await svc.SaveAsync(ownerId, "ownerSave", null, model, saveExecutionState: false, groupId: group.Id);
        savedByOwner.ShouldNotBeNull();
        savedByOwner.UserId.ShouldBe(ownerId);
    }

    #region DeleteGroupAsync Tests

    [Fact]
    public async Task DeleteGroupAsync_OwnerCanDeleteGroup()
    {
        using var db = CreateInMemoryDb("delete_group_owner");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var ownerId = "owner-del-1";
        var group = await svc.CreateGroupAsync(ownerId, "ToDelete", "desc");

        group.ShouldNotBeNull();

        await svc.DeleteGroupAsync(group.Id, ownerId);

        var deletedGroup = await svc.GetGroupAsync(group.Id);
        deletedGroup.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteGroupAsync_NonOwnerCannotDelete()
    {
        using var db = CreateInMemoryDb("delete_group_nonowner");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var ownerId = "owner-del-2";
        var otherId = "other-user";
        var group = await svc.CreateGroupAsync(ownerId, "Protected", null);

        await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
            await svc.DeleteGroupAsync(group.Id, otherId));

        var stillExists = await svc.GetGroupAsync(group.Id);
        stillExists.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteGroupAsync_ClearsAssignments()
    {
        using var db = CreateInMemoryDb("delete_group_clear_assignments");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var ownerId = "owner-del-3";
        var group = await svc.CreateGroupAsync(ownerId, "GroupWithAutomatons", null);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        var automaton = await svc.SaveAsync(ownerId, "auto1", null, model, false, group.Id);

        var assignmentsBefore = await db.SavedAutomatonGroupAssignments
            .Where(a => a.GroupId == group.Id)
            .ToListAsync();
        assignmentsBefore.Count.ShouldBe(1);

        await svc.DeleteGroupAsync(group.Id, ownerId);

        var assignmentsAfter = await db.SavedAutomatonGroupAssignments
            .Where(a => a.GroupId == group.Id)
            .ToListAsync();
        assignmentsAfter.ShouldBeEmpty();

        var automatonStillExists = await svc.GetAsync(automaton.Id, ownerId);
        automatonStillExists.ShouldNotBeNull();
    }

    [Fact]
    public async Task DeleteGroupAsync_NonExistentGroup_DoesNotThrow()
    {
        using var db = CreateInMemoryDb("delete_group_nonexistent");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        await svc.DeleteGroupAsync(99999, "any-user");
    }

    #endregion

    #region AssignAutomatonToGroupAsync Tests

    [Fact]
    public async Task AssignAutomatonToGroupAsync_OwnerCanAssign()
    {
        using var db = CreateInMemoryDb("assign_owner");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var userId = "user-assign-1";
        var group = await svc.CreateGroupAsync(userId, "TargetGroup", null);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        var automaton = await svc.SaveAsync(userId, "auto", null, model, false);

        await svc.AssignAutomatonToGroupAsync(automaton.Id, userId, group.Id);

        var assignments = await db.SavedAutomatonGroupAssignments
            .Where(a => a.AutomatonId == automaton.Id)
            .ToListAsync();
        assignments.Count.ShouldBe(1);
        assignments[0].GroupId.ShouldBe(group.Id);
    }

    [Fact]
    public async Task AssignAutomatonToGroupAsync_MemberCanAssignWhenMembersCanShare()
    {
        using var db = CreateInMemoryDb("assign_member");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var ownerId = "owner-assign-2";
        var memberId = "member-assign-2";
        var group = await svc.CreateGroupAsync(ownerId, "SharedGroup", null);

        await svc.AddGroupMemberAsync(group.Id, memberId);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        var automaton = await svc.SaveAsync(memberId, "member-auto", null, model, false);

        await svc.AssignAutomatonToGroupAsync(automaton.Id, memberId, group.Id);

        var assignments = await db.SavedAutomatonGroupAssignments
            .Where(a => a.AutomatonId == automaton.Id)
            .ToListAsync();
        assignments.Count.ShouldBe(1);
        assignments[0].GroupId.ShouldBe(group.Id);
    }

    [Fact]
    public async Task AssignAutomatonToGroupAsync_NonMemberCannotAssign()
    {
        using var db = CreateInMemoryDb("assign_nonmember");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var ownerId = "owner-assign-3";
        var outsiderId = "outsider-assign-3";
        var group = await svc.CreateGroupAsync(ownerId, "PrivateGroup", null);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        var automaton = await svc.SaveAsync(outsiderId, "outsider-auto", null, model, false);

        await Should.ThrowAsync<UnauthorizedAccessException>(async () =>
            await svc.AssignAutomatonToGroupAsync(automaton.Id, outsiderId, group.Id));
    }

    [Fact]
    public async Task AssignAutomatonToGroupAsync_NullGroupId_RemovesAllAssignments()
    {
        using var db = CreateInMemoryDb("assign_null");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var userId = "user-assign-4";
        var group1 = await svc.CreateGroupAsync(userId, "Group1", null);
        var group2 = await svc.CreateGroupAsync(userId, "Group2", null);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        var automaton = await svc.SaveAsync(userId, "auto", null, model, false, group1.Id);
        await svc.AssignAutomatonToGroupAsync(automaton.Id, userId, group2.Id);

        var assignmentsBefore = await db.SavedAutomatonGroupAssignments
            .Where(a => a.AutomatonId == automaton.Id)
            .ToListAsync();
        assignmentsBefore.Count.ShouldBe(2);

        await svc.AssignAutomatonToGroupAsync(automaton.Id, userId, null);

        var assignmentsAfter = await db.SavedAutomatonGroupAssignments
            .Where(a => a.AutomatonId == automaton.Id)
            .ToListAsync();
        assignmentsAfter.ShouldBeEmpty();
    }

    [Fact]
    public async Task AssignAutomatonToGroupAsync_DuplicateAssignment_DoesNotDuplicate()
    {
        using var db = CreateInMemoryDb("assign_duplicate");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var userId = "user-assign-5";
        var group = await svc.CreateGroupAsync(userId, "Group", null);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        var automaton = await svc.SaveAsync(userId, "auto", null, model, false);

        await svc.AssignAutomatonToGroupAsync(automaton.Id, userId, group.Id);
        await svc.AssignAutomatonToGroupAsync(automaton.Id, userId, group.Id);

        var assignments = await db.SavedAutomatonGroupAssignments
            .Where(a => a.AutomatonId == automaton.Id)
            .ToListAsync();
        assignments.Count.ShouldBe(1);
    }

    [Fact]
    public async Task AssignAutomatonToGroupAsync_NonExistentAutomaton_Throws()
    {
        using var db = CreateInMemoryDb("assign_nonexistent_auto");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var userId = "user-assign-6";
        var group = await svc.CreateGroupAsync(userId, "Group", null);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await svc.AssignAutomatonToGroupAsync(99999, userId, group.Id));
    }

    [Fact]
    public async Task AssignAutomatonToGroupAsync_NonExistentGroup_Throws()
    {
        using var db = CreateInMemoryDb("assign_nonexistent_group");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var userId = "user-assign-7";
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        var automaton = await svc.SaveAsync(userId, "auto", null, model, false);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await svc.AssignAutomatonToGroupAsync(automaton.Id, userId, 99999));
    }

    #endregion

    #region RemoveAutomatonFromGroupAsync Tests

    [Fact]
    public async Task RemoveAutomatonFromGroupAsync_RemovesAssignment()
    {
        using var db = CreateInMemoryDb("remove_assignment");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var userId = "user-remove-1";
        var group = await svc.CreateGroupAsync(userId, "Group", null);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        var automaton = await svc.SaveAsync(userId, "auto", null, model, false, group.Id);

        var assignmentsBefore = await db.SavedAutomatonGroupAssignments
            .Where(a => a.AutomatonId == automaton.Id && a.GroupId == group.Id)
            .ToListAsync();
        assignmentsBefore.Count.ShouldBe(1);

        await svc.RemoveAutomatonFromGroupAsync(automaton.Id, userId, group.Id);

        var assignmentsAfter = await db.SavedAutomatonGroupAssignments
            .Where(a => a.AutomatonId == automaton.Id && a.GroupId == group.Id)
            .ToListAsync();
        assignmentsAfter.ShouldBeEmpty();
    }

    [Fact]
    public async Task RemoveAutomatonFromGroupAsync_OnlyRemovesSpecificAssignment()
    {
        using var db = CreateInMemoryDb("remove_specific");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var userId = "user-remove-2";
        var group1 = await svc.CreateGroupAsync(userId, "Group1", null);
        var group2 = await svc.CreateGroupAsync(userId, "Group2", null);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        var automaton = await svc.SaveAsync(userId, "auto", null, model, false, group1.Id);
        await svc.AssignAutomatonToGroupAsync(automaton.Id, userId, group2.Id);

        await svc.RemoveAutomatonFromGroupAsync(automaton.Id, userId, group1.Id);

        var allAssignments = await db.SavedAutomatonGroupAssignments
            .Where(a => a.AutomatonId == automaton.Id)
            .ToListAsync();
        allAssignments.Count.ShouldBe(1);
        allAssignments[0].GroupId.ShouldBe(group2.Id);
    }

    [Fact]
    public async Task RemoveAutomatonFromGroupAsync_NonExistentAssignment_DoesNotThrow()
    {
        using var db = CreateInMemoryDb("remove_nonexistent");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var userId = "user-remove-3";
        var group = await svc.CreateGroupAsync(userId, "Group", null);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        var automaton = await svc.SaveAsync(userId, "auto", null, model, false);

        await svc.RemoveAutomatonFromGroupAsync(automaton.Id, userId, group.Id);
    }

    [Fact]
    public async Task RemoveAutomatonFromGroupAsync_WrongUser_Throws()
    {
        using var db = CreateInMemoryDb("remove_wrong_user");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var userId = "user-remove-4";
        var otherId = "other-user";
        var group = await svc.CreateGroupAsync(userId, "Group", null);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        var automaton = await svc.SaveAsync(userId, "auto", null, model, false, group.Id);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await svc.RemoveAutomatonFromGroupAsync(automaton.Id, otherId, group.Id));
    }

    #endregion

    #region CreateGroupAsync Additional Tests

    [Fact]
    public async Task CreateGroupAsync_DuplicateName_SameUser_Throws()
    {
        using var db = CreateInMemoryDb("create_duplicate");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var userId = "user-dup-1";
        await svc.CreateGroupAsync(userId, "MyGroup", "first");

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await svc.CreateGroupAsync(userId, "MyGroup", "second"));
    }

    [Fact]
    public async Task CreateGroupAsync_DuplicateName_CaseInsensitive_Throws()
    {
        using var db = CreateInMemoryDb("create_duplicate_case");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var userId = "user-dup-2";
        await svc.CreateGroupAsync(userId, "MyGroup", null);

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await svc.CreateGroupAsync(userId, "mygroup", null));

        await Should.ThrowAsync<InvalidOperationException>(async () =>
            await svc.CreateGroupAsync(userId, "MYGROUP", null));
    }

    [Fact]
    public async Task CreateGroupAsync_DuplicateName_DifferentUser_Succeeds()
    {
        using var db = CreateInMemoryDb("create_duplicate_diffuser");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var user1 = "user1";
        var user2 = "user2";

        var group1 = await svc.CreateGroupAsync(user1, "SharedName", null);
        var group2 = await svc.CreateGroupAsync(user2, "SharedName", null);

        group1.ShouldNotBeNull();
        group2.ShouldNotBeNull();
        group1.Id.ShouldNotBe(group2.Id);
        group1.UserId.ShouldBe(user1);
        group2.UserId.ShouldBe(user2);
    }

    [Fact]
    public async Task CreateGroupAsync_TrimsName()
    {
        using var db = CreateInMemoryDb("create_trim");
        var svc = new SavedAutomatonService(new NullLogger<SavedAutomatonService>(), db);

        var userId = "user-trim";
        var group = await svc.CreateGroupAsync(userId, "  Trimmed  ", null);

        group.Name.ShouldBe("Trimmed");
    }

    #endregion
}
