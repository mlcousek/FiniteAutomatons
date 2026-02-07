using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Data;
using FiniteAutomatons.Services.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

public class SharedAutomatonServiceTests : IDisposable
{
    private readonly ApplicationDbContext context;
    private readonly SharedAutomatonService service;
    private const string User1Id = "user1@test.com";
    private const string User2Id = "user2@test.com";
    private const string User3Id = "user3@test.com";

    public SharedAutomatonServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        context = new ApplicationDbContext(options);
        service = new SharedAutomatonService(context, NullLogger<SharedAutomatonService>.Instance);
    }

    public void Dispose()
    {
        context.Database.EnsureDeleted();
        context.Dispose();
        GC.SuppressFinalize(this);
    }

    #region CRUD Operations Tests

    [Fact]
    public async Task SaveAsync_ValidInput_Success()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Contributor);
        var model = CreateSampleAutomatonViewModel();

        // Act
        var result = await service.SaveAsync(User1Id, group.Id, "Test Automaton", "Description", model);

        // Assert
        result.ShouldNotBeNull();
        result.Name.ShouldBe("Test Automaton");
        result.Description.ShouldBe("Description");
        result.CreatedByUserId.ShouldBe(User1Id);
        result.SaveMode.ShouldBe(AutomatonSaveMode.Structure);
        
        var assignments = await context.SharedAutomatonGroupAssignments.Where(a => a.AutomatonId == result.Id).ToListAsync();
        assignments.Count.ShouldBe(1);
        assignments[0].GroupId.ShouldBe(group.Id);
    }

    [Fact]
    public async Task SaveAsync_NoPermission_ThrowsUnauthorized()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Viewer);
        var model = CreateSampleAutomatonViewModel();

        // Act & Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(
            async () => await service.SaveAsync(User1Id, group.Id, "Test", null, model));
    }

    [Fact]
    public async Task GetAsync_UserHasAccess_ReturnsAutomaton()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Contributor);
        var automaton = await SaveAutomatonToGroup(User1Id, group.Id);

        // Act
        var result = await service.GetAsync(automaton.Id, User1Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(automaton.Id);
        result.Name.ShouldBe(automaton.Name);
    }

    [Fact]
    public async Task GetAsync_UserNoAccess_ReturnsNull()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Editor);
        var automaton = await SaveAutomatonToGroup(User1Id, group.Id);

        // Act
        var result = await service.GetAsync(automaton.Id, User2Id);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ListForGroupAsync_ValidGroup_ReturnsAll()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Contributor);
        await SaveAutomatonToGroup(User1Id, group.Id, "Automaton 1");
        await SaveAutomatonToGroup(User1Id, group.Id, "Automaton 2");
        await SaveAutomatonToGroup(User1Id, group.Id, "Automaton 3");

        // Act
        var result = await service.ListForGroupAsync(group.Id, User1Id);

        // Assert
        result.Count.ShouldBe(3);
        result.Select(a => a.Name).ShouldContain("Automaton 1");
        result.Select(a => a.Name).ShouldContain("Automaton 2");
        result.Select(a => a.Name).ShouldContain("Automaton 3");
    }

    [Fact]
    public async Task DeleteAsync_AsCreator_Success()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Contributor);
        var automaton = await SaveAutomatonToGroup(User1Id, group.Id);

        // Act
        await service.DeleteAsync(automaton.Id, User1Id);

        // Assert
        var deleted = await context.SharedAutomatons.FindAsync(automaton.Id);
        deleted.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_AsEditor_Success()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Editor);
        var automaton = await SaveAutomatonToGroup(User1Id, group.Id);

        // Act
        await service.DeleteAsync(automaton.Id, User2Id);

        // Assert
        var deleted = await context.SharedAutomatons.FindAsync(automaton.Id);
        deleted.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteAsync_AsViewer_ThrowsUnauthorized()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Viewer);
        var automaton = await SaveAutomatonToGroup(User1Id, group.Id);

        // Act & Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(
            async () => await service.DeleteAsync(automaton.Id, User2Id));
    }

    [Fact]
    public async Task UpdateAsync_ValidChanges_Success()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Editor);
        var automaton = await SaveAutomatonToGroup(User1Id, group.Id);

        // Act
        var result = await service.UpdateAsync(automaton.Id, User1Id, "Updated Name", "Updated Desc", null);

        // Assert
        result.Name.ShouldBe("Updated Name");
        result.Description.ShouldBe("Updated Desc");
        result.ModifiedAt.ShouldNotBeNull();
        result.ModifiedByUserId.ShouldBe(User1Id);
    }

    [Fact]
    public async Task UpdateAsync_NoPermission_ThrowsUnauthorized()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Viewer);
        var automaton = await SaveAutomatonToGroup(User1Id, group.Id);

        // Act & Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(
            async () => await service.UpdateAsync(automaton.Id, User2Id, "New Name", null, null));
    }

    #endregion

    #region Group Management Tests

    [Fact]
    public async Task CreateGroupAsync_ValidData_CreatesGroupAndOwner()
    {
        // Act
        var group = await service.CreateGroupAsync(User1Id, "My Group", "Test Description");

        // Assert
        group.ShouldNotBeNull();
        group.Name.ShouldBe("My Group");
        group.Description.ShouldBe("Test Description");
        group.UserId.ShouldBe(User1Id);

        var members = await context.SharedAutomatonGroupMembers
            .Where(m => m.GroupId == group.Id)
            .ToListAsync();
        
        members.Count.ShouldBe(1);
        members[0].UserId.ShouldBe(User1Id);
        members[0].Role.ShouldBe(SharedGroupRole.Owner);
    }

    [Fact]
    public async Task GetGroupAsync_AsMember_ReturnsGroup()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Viewer);

        // Act
        var result = await service.GetGroupAsync(group.Id, User1Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(group.Id);
        result.Name.ShouldBe(group.Name);
    }

    [Fact]
    public async Task GetGroupAsync_NotMember_ReturnsNull()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);

        // Act
        var result = await service.GetGroupAsync(group.Id, User2Id);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task ListGroupsForUserAsync_ReturnsOnlyMemberGroups()
    {
        // Arrange
        var group1 = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);
        var group2 = await CreateGroupWithMember(User1Id, SharedGroupRole.Editor);
        await CreateGroupWithMember(User2Id, SharedGroupRole.Owner); // User1 not a member

        // Act
        var result = await service.ListGroupsForUserAsync(User1Id);

        // Assert
        result.Count.ShouldBe(2);
        result.Select(g => g.Id).ShouldContain(group1.Id);
        result.Select(g => g.Id).ShouldContain(group2.Id);
    }

    [Fact]
    public async Task DeleteGroupAsync_AsOwner_Success()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);

        // Act
        await service.DeleteGroupAsync(group.Id, User1Id);

        // Assert
        var deleted = await context.SharedAutomatonGroups.FindAsync(group.Id);
        deleted.ShouldBeNull();
    }

    [Fact]
    public async Task DeleteGroupAsync_NotOwner_ThrowsUnauthorized()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Admin);

        // Act & Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(
            async () => await service.DeleteGroupAsync(group.Id, User2Id));
    }

    [Fact]
    public async Task UpdateGroupAsync_AsAdmin_Success()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Admin);

        // Act
        await service.UpdateGroupAsync(group.Id, User2Id, "New Name", "New Description");

        // Assert
        var updated = await context.SharedAutomatonGroups.FindAsync(group.Id);
        updated.ShouldNotBeNull();
        updated.Name.ShouldBe("New Name");
        updated.Description.ShouldBe("New Description");
    }

    [Fact]
    public async Task UpdateGroupAsync_AsViewer_ThrowsUnauthorized()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Viewer);

        // Act & Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(
            async () => await service.UpdateGroupAsync(group.Id, User2Id, "New Name", null));
    }

    #endregion

    #region Member Management Tests

    [Fact]
    public async Task ListGroupMembersAsync_ValidGroup_ReturnsSortedByRole()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Viewer);
        await AddMemberToGroup(group.Id, User3Id, SharedGroupRole.Editor);

        // Act
        var result = await service.ListGroupMembersAsync(group.Id, User1Id);

        // Assert
        result.Count.ShouldBe(3);
        result[0].Role.ShouldBe(SharedGroupRole.Owner); // Highest role first
        result[1].Role.ShouldBe(SharedGroupRole.Editor);
        result[2].Role.ShouldBe(SharedGroupRole.Viewer);
    }

    [Fact]
    public async Task RemoveMemberAsync_AsAdmin_Success()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Admin);
        await AddMemberToGroup(group.Id, User3Id, SharedGroupRole.Viewer);

        // Act
        await service.RemoveMemberAsync(group.Id, User2Id, User3Id);

        // Assert
        var members = await context.SharedAutomatonGroupMembers
            .Where(m => m.GroupId == group.Id)
            .ToListAsync();
        
        members.Count.ShouldBe(2);
        members.ShouldNotContain(m => m.UserId == User3Id);
    }

    [Fact]
    public async Task RemoveMemberAsync_CannotRemoveOwner()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Admin);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await service.RemoveMemberAsync(group.Id, User2Id, User1Id));
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_ValidChange_Success()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Viewer);

        // Act
        await service.UpdateMemberRoleAsync(group.Id, User1Id, User2Id, SharedGroupRole.Editor);

        // Assert
        var member = await context.SharedAutomatonGroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == group.Id && m.UserId == User2Id);
        
        member.ShouldNotBeNull();
        member.Role.ShouldBe(SharedGroupRole.Editor);
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_CannotChangeOwnerRole()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Admin);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await service.UpdateMemberRoleAsync(group.Id, User2Id, User1Id, SharedGroupRole.Admin));
    }

    [Fact]
    public async Task UpdateMemberRoleAsync_NoPermission_ThrowsUnauthorized()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Viewer);
        await AddMemberToGroup(group.Id, User3Id, SharedGroupRole.Contributor);

        // Act & Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(
            async () => await service.UpdateMemberRoleAsync(group.Id, User2Id, User3Id, SharedGroupRole.Editor));
    }

    #endregion

    #region Permission Checks Tests

    [Fact]
    public async Task CanUserViewGroupAsync_AsMember_ReturnsTrue()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Viewer);

        // Act
        var result = await service.CanUserViewGroupAsync(group.Id, User1Id);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CanUserAddToGroupAsync_AsContributor_ReturnsTrue()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Contributor);

        // Act
        var result = await service.CanUserAddToGroupAsync(group.Id, User1Id);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CanUserAddToGroupAsync_AsViewer_ReturnsFalse()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Viewer);

        // Act
        var result = await service.CanUserAddToGroupAsync(group.Id, User1Id);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task CanUserEditInGroupAsync_AsEditor_ReturnsTrue()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Editor);

        // Act
        var result = await service.CanUserEditInGroupAsync(group.Id, User1Id);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task CanUserManageMembersAsync_AsAdmin_ReturnsTrue()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);

        // Act
        var result = await service.CanUserManageMembersAsync(group.Id, User1Id);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task GetUserRoleInGroupAsync_ReturnsCorrectRole()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Editor);

        // Act
        var role1 = await service.GetUserRoleInGroupAsync(group.Id, User1Id);
        var role2 = await service.GetUserRoleInGroupAsync(group.Id, User2Id);
        var role3 = await service.GetUserRoleInGroupAsync(group.Id, User3Id);

        // Assert
        role1.ShouldBe(SharedGroupRole.Owner);
        role2.ShouldBe(SharedGroupRole.Editor);
        role3.ShouldBeNull();
    }

    #endregion

    #region Helper Methods

    private async Task<SharedAutomatonGroup> CreateGroupWithMember(string userId, SharedGroupRole role)
    {
        var group = new SharedAutomatonGroup
        {
            UserId = userId,
            Name = $"Group_{Guid.NewGuid():N}",
            Description = "Test group",
            CreatedAt = DateTime.UtcNow
        };

        context.SharedAutomatonGroups.Add(group);
        await context.SaveChangesAsync();

        var member = new SharedAutomatonGroupMember
        {
            GroupId = group.Id,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };

        context.SharedAutomatonGroupMembers.Add(member);
        await context.SaveChangesAsync();

        return group;
    }

    private async Task AddMemberToGroup(int groupId, string userId, SharedGroupRole role)
    {
        var member = new SharedAutomatonGroupMember
        {
            GroupId = groupId,
            UserId = userId,
            Role = role,
            JoinedAt = DateTime.UtcNow
        };

        context.SharedAutomatonGroupMembers.Add(member);
        await context.SaveChangesAsync();
    }

    private async Task<SharedAutomaton> SaveAutomatonToGroup(string userId, int groupId, string? name = null)
    {
        var model = CreateSampleAutomatonViewModel();
        return await service.SaveAsync(userId, groupId, name ?? "Test Automaton", "Description", model);
    }

    private static AutomatonViewModel CreateSampleAutomatonViewModel()
    {
        return new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new Core.Models.DoMain.State { Id = 0, IsStart = true, IsAccepting = false },
                new Core.Models.DoMain.State { Id = 1, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new Core.Models.DoMain.Transition { FromStateId = 0, ToStateId = 1, Symbol = 'a' }
            ],
            IsCustomAutomaton = true
        };
    }

    #endregion
}
