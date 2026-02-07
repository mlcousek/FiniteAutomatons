using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Data;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.Security.Claims;

namespace FiniteAutomatons.UnitTests.Services;

public class SharedAutomatonSharingServiceTests : IDisposable
{
    private readonly ApplicationDbContext context;
    private readonly SharedAutomatonService sharedAutomatonService;
    private readonly SharedAutomatonSharingService sharingService;
    private readonly TestUserManager userManager;

    private const string User1Id = "user1@test.com";
    private const string User2Id = "user2@test.com";
    private const string User1Email = "user1@test.com";
    private const string User2Email = "user2@test.com";

    // Test UserManager implementation
    private sealed class FakeUserStore : IUserStore<IdentityUser>
    {
        public Task<IdentityResult> CreateAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public void Dispose() { }
        public Task<IdentityUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<IdentityUser?>(null);
        public Task<IdentityUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<IdentityUser?>(null);
        public Task<string?> GetNormalizedUserNameAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.UserName?.ToUpperInvariant());
        public Task<string> GetUserIdAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult<string>(user.Id);
        public Task<string?> GetUserNameAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.UserName);
        public Task SetNormalizedUserNameAsync(IdentityUser user, string? normalizedName, CancellationToken cancellationToken) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }
        public Task SetUserNameAsync(IdentityUser user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
        public Task<IdentityResult> UpdateAsync(IdentityUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    }

    private class TestUserManager() : UserManager<IdentityUser>(
        new FakeUserStore(),
        Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
        new PasswordHasher<IdentityUser>(),
        [],
        [],
        new UpperInvariantLookupNormalizer(),
        new IdentityErrorDescriber(),
        services: null!,
        logger: new NullLogger<UserManager<IdentityUser>>())
    {
        private readonly Dictionary<string, IdentityUser> users = [];

        public void AddTestUser(string userId, string email)
        {
            users[userId] = new IdentityUser { Id = userId, Email = email, UserName = email };
        }

        public override Task<IdentityUser?> FindByIdAsync(string userId)
        {
            users.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }

        public override Task<IdentityUser?> GetUserAsync(ClaimsPrincipal principal)
        {
            var userId = principal.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (userId != null && users.TryGetValue(userId, out var user))
            {
                return Task.FromResult<IdentityUser?>(user);
            }
            return Task.FromResult<IdentityUser?>(null);
        }
    }

    public SharedAutomatonSharingServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        context = new ApplicationDbContext(options);
        sharedAutomatonService = new SharedAutomatonService(context, NullLogger<SharedAutomatonService>.Instance);

        userManager = new TestUserManager();

        sharingService = new SharedAutomatonSharingService(
            context,
            sharedAutomatonService,
            userManager,
            NullLogger<SharedAutomatonSharingService>.Instance);
    }

    public void Dispose()
    {
        context.Database.EnsureDeleted();
        context.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Email Invitations Tests

    [Fact]
    public async Task InviteByEmailAsync_ValidInput_CreatesInvitation()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);

        // Act
        var invitation = await sharingService.InviteByEmailAsync(
            group.Id, User1Id, User2Email, SharedGroupRole.Contributor, 7);

        // Assert
        invitation.ShouldNotBeNull();
        invitation.GroupId.ShouldBe(group.Id);
        invitation.Email.ShouldBe(User2Email.ToLowerInvariant());
        invitation.Role.ShouldBe(SharedGroupRole.Contributor);
        invitation.InvitedByUserId.ShouldBe(User1Id);
        invitation.Status.ShouldBe(InvitationStatus.Pending);
        invitation.Token.ShouldNotBeNullOrEmpty();
        invitation.ExpiresAt.ShouldNotBeNull();
        invitation.ExpiresAt.Value.ShouldBeGreaterThan(DateTime.UtcNow);
    }

    [Fact]
    public async Task InviteByEmailAsync_NoPermission_ThrowsUnauthorized()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Viewer);

        // Act & Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(
            async () => await sharingService.InviteByEmailAsync(
                group.Id, User2Id, "newuser@test.com", SharedGroupRole.Contributor));
    }

    [Fact]
    public async Task InviteByEmailAsync_GeneratesUniqueToken()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);

        // Act
        var invitation1 = await sharingService.InviteByEmailAsync(
            group.Id, User1Id, "user1@example.com", SharedGroupRole.Viewer);
        var invitation2 = await sharingService.InviteByEmailAsync(
            group.Id, User1Id, "user2@example.com", SharedGroupRole.Viewer);

        // Assert
        invitation1.Token.ShouldNotBe(invitation2.Token);
        invitation1.Token.Length.ShouldBeGreaterThan(20); // Tokens should be reasonably long
        invitation2.Token.Length.ShouldBeGreaterThan(20);
    }

    [Fact]
    public async Task InviteByEmailAsync_InvalidEmail_ThrowsArgumentException()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);

        // Act & Assert
        await Should.ThrowAsync<ArgumentException>(
            async () => await sharingService.InviteByEmailAsync(
                group.Id, User1Id, "not-an-email", SharedGroupRole.Viewer));
    }

    [Fact]
    public async Task InviteByEmailAsync_AlreadyMember_ThrowsInvalidOperation()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Viewer);

        // Act & Assert - Cannot invite existing member
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sharingService.InviteByEmailAsync(
                group.Id, User1Id, User2Id, SharedGroupRole.Editor));
    }

    [Fact]
    public async Task AcceptInvitationAsync_ValidToken_CreatesMembership()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);
        var invitation = await sharingService.InviteByEmailAsync(
            group.Id, User1Id, User2Email, SharedGroupRole.Contributor);

        SetupUserManagerForUser(User2Id, User2Email);

        // Act
        await sharingService.AcceptInvitationAsync(invitation.Token, User2Id);

        // Assert
        var members = await context.SharedAutomatonGroupMembers
            .Where(m => m.GroupId == group.Id)
            .ToListAsync();

        members.Count.ShouldBe(2);
        var newMember = members.FirstOrDefault(m => m.UserId == User2Id);
        newMember.ShouldNotBeNull();
        newMember.Role.ShouldBe(SharedGroupRole.Contributor);
        newMember.InvitedByUserId.ShouldBe(User1Id);

        var updatedInvitation = await context.SharedAutomatonGroupInvitations.FindAsync(invitation.Id);
        updatedInvitation.ShouldNotBeNull();
        updatedInvitation.Status.ShouldBe(InvitationStatus.Accepted);
    }

    [Fact]
    public async Task AcceptInvitationAsync_ExpiredToken_ThrowsException()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);
        var invitation = await sharingService.InviteByEmailAsync(
            group.Id, User1Id, User2Email, SharedGroupRole.Viewer, 1);

        // Manually expire the invitation
        invitation.ExpiresAt = DateTime.UtcNow.AddDays(-1);
        await context.SaveChangesAsync();

        SetupUserManagerForUser(User2Id, User2Email);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sharingService.AcceptInvitationAsync(invitation.Token, User2Id));
    }

    [Fact]
    public async Task AcceptInvitationAsync_AlreadyMember_ThrowsException()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);

        // Create invitation for User2
        var invitation = await sharingService.InviteByEmailAsync(
            group.Id, User1Id, User2Email, SharedGroupRole.Contributor);

        SetupUserManagerForUser(User2Id, User2Email);

        // Accept the invitation (User2 becomes a member)
        await sharingService.AcceptInvitationAsync(invitation.Token, User2Id);

        // Act & Assert - Try to accept the same invitation again (User2 is already a member)
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sharingService.AcceptInvitationAsync(invitation.Token, User2Id));
    }

    [Fact]
    public async Task AcceptInvitationAsync_EmailMismatch_ThrowsUnauthorized()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);
        var invitation = await sharingService.InviteByEmailAsync(
            group.Id, User1Id, "correct@test.com", SharedGroupRole.Viewer);

        SetupUserManagerForUser(User2Id, "wrong@test.com");

        // Act & Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(
            async () => await sharingService.AcceptInvitationAsync(invitation.Token, User2Id));
    }

    [Fact]
    public async Task DeclineInvitationAsync_ValidToken_MarksDeclined()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);
        var invitation = await sharingService.InviteByEmailAsync(
            group.Id, User1Id, User2Email, SharedGroupRole.Viewer);

        // Act
        await sharingService.DeclineInvitationAsync(invitation.Token);

        // Assert
        var declined = await context.SharedAutomatonGroupInvitations.FindAsync(invitation.Id);
        declined.ShouldNotBeNull();
        declined.Status.ShouldBe(InvitationStatus.Declined);
        declined.ResponsedAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task CancelInvitationAsync_AsAdmin_Success()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);
        var invitation = await sharingService.InviteByEmailAsync(
            group.Id, User1Id, User2Email, SharedGroupRole.Viewer);

        // Act
        await sharingService.CancelInvitationAsync(invitation.Id, User1Id);

        // Assert
        var cancelled = await context.SharedAutomatonGroupInvitations.FindAsync(invitation.Id);
        cancelled.ShouldNotBeNull();
        cancelled.Status.ShouldBe(InvitationStatus.Cancelled);
    }

    [Fact]
    public async Task CancelInvitationAsync_NoPermission_ThrowsUnauthorized()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Viewer);

        var invitation = await sharingService.InviteByEmailAsync(
            group.Id, User1Id, "invited@test.com", SharedGroupRole.Viewer);

        // Act & Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(
            async () => await sharingService.CancelInvitationAsync(invitation.Id, User2Id));
    }

    [Fact]
    public async Task ListPendingInvitationsAsync_ReturnsOnlyPending()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);

        var pending1 = await sharingService.InviteByEmailAsync(
            group.Id, User1Id, "pending1@test.com", SharedGroupRole.Viewer);
        var pending2 = await sharingService.InviteByEmailAsync(
            group.Id, User1Id, "pending2@test.com", SharedGroupRole.Contributor);
        var declined = await sharingService.InviteByEmailAsync(
            group.Id, User1Id, "declined@test.com", SharedGroupRole.Viewer);

        await sharingService.DeclineInvitationAsync(declined.Token);

        // Act
        var result = await sharingService.ListPendingInvitationsAsync(group.Id, User1Id);

        // Assert
        result.Count.ShouldBe(2);
        result.Select(i => i.Id).ShouldContain(pending1.Id);
        result.Select(i => i.Id).ShouldContain(pending2.Id);
        result.Select(i => i.Id).ShouldNotContain(declined.Id);
    }

    [Fact]
    public async Task GetInvitationByTokenAsync_FindsCorrectInvitation()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);
        var invitation = await sharingService.InviteByEmailAsync(
            group.Id, User1Id, User2Email, SharedGroupRole.Editor);

        // Act
        var found = await sharingService.GetInvitationByTokenAsync(invitation.Token);

        // Assert
        found.ShouldNotBeNull();
        found.Id.ShouldBe(invitation.Id);
        found.Email.ShouldBe(User2Email.ToLowerInvariant());
        found.Role.ShouldBe(SharedGroupRole.Editor);
        found.Group.ShouldNotBeNull();
    }

    #endregion

    #region Shareable Links Tests

    [Fact]
    public async Task GenerateInviteLinkAsync_CreatesUniqueCode()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);

        // Act
        var inviteCode = await sharingService.GenerateInviteLinkAsync(
            group.Id, User1Id, SharedGroupRole.Viewer, 30);

        // Assert
        inviteCode.ShouldNotBeNullOrEmpty();
        inviteCode.Length.ShouldBe(8);
        inviteCode.ShouldMatch(@"^[A-Z0-9]+$"); // Only uppercase letters and numbers

        var updatedGroup = await context.SharedAutomatonGroups.FindAsync(group.Id);
        updatedGroup.ShouldNotBeNull();
        updatedGroup.InviteCode.ShouldBe(inviteCode);
        updatedGroup.IsInviteLinkActive.ShouldBeTrue();
        updatedGroup.DefaultRoleForInvite.ShouldBe(SharedGroupRole.Viewer);
        updatedGroup.InviteLinkExpiresAt.ShouldNotBeNull();
    }

    [Fact]
    public async Task GenerateInviteLinkAsync_NoExpiration_SetsNullExpiry()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);

        // Act
        await sharingService.GenerateInviteLinkAsync(
            group.Id, User1Id, SharedGroupRole.Contributor, null);

        // Assert
        var updatedGroup = await context.SharedAutomatonGroups.FindAsync(group.Id);
        updatedGroup.ShouldNotBeNull();
        updatedGroup.InviteLinkExpiresAt.ShouldBeNull();
    }

    [Fact]
    public async Task GenerateInviteLinkAsync_NoPermission_ThrowsUnauthorized()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Contributor);

        // Act & Assert
        await Should.ThrowAsync<UnauthorizedAccessException>(
            async () => await sharingService.GenerateInviteLinkAsync(
                group.Id, User2Id, SharedGroupRole.Viewer));
    }

    [Fact]
    public async Task GetInviteLinkAsync_ActiveLink_ReturnsCode()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);
        var code = await sharingService.GenerateInviteLinkAsync(
            group.Id, User1Id, SharedGroupRole.Viewer, 30);

        // Act
        var result = await sharingService.GetInviteLinkAsync(group.Id, User1Id);

        // Assert
        result.ShouldBe(code);
    }

    [Fact]
    public async Task GetInviteLinkAsync_NoLink_ReturnsNull()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);

        // Act
        var result = await sharingService.GetInviteLinkAsync(group.Id, User1Id);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task GetInviteLinkAsync_InactiveLink_ReturnsNull()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);
        await sharingService.GenerateInviteLinkAsync(group.Id, User1Id, SharedGroupRole.Viewer);
        await sharingService.DeactivateInviteLinkAsync(group.Id, User1Id);

        // Act
        var result = await sharingService.GetInviteLinkAsync(group.Id, User1Id);

        // Assert
        result.ShouldBeNull();
    }

    [Fact]
    public async Task DeactivateInviteLinkAsync_MarksInactive()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);
        await sharingService.GenerateInviteLinkAsync(group.Id, User1Id, SharedGroupRole.Viewer);

        // Act
        await sharingService.DeactivateInviteLinkAsync(group.Id, User1Id);

        // Assert
        var updatedGroup = await context.SharedAutomatonGroups.FindAsync(group.Id);
        updatedGroup.ShouldNotBeNull();
        updatedGroup.IsInviteLinkActive.ShouldBeFalse();
    }

    [Fact]
    public async Task JoinViaInviteLinkAsync_ValidCode_Success()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Owner);
        var code = await sharingService.GenerateInviteLinkAsync(
            group.Id, User1Id, SharedGroupRole.Contributor, 30);

        // Act
        var result = await sharingService.JoinViaInviteLinkAsync(code, User2Id);

        // Assert
        result.ShouldNotBeNull();
        result.Id.ShouldBe(group.Id);

        var members = await context.SharedAutomatonGroupMembers
            .Where(m => m.GroupId == group.Id && m.UserId == User2Id)
            .ToListAsync();

        members.Count.ShouldBe(1);
        members[0].Role.ShouldBe(SharedGroupRole.Contributor);
        members[0].InvitedByUserId.ShouldBeNull(); // Joined via link
    }

    [Fact]
    public async Task JoinViaInviteLinkAsync_ExpiredLink_ThrowsException()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);
        var code = await sharingService.GenerateInviteLinkAsync(
            group.Id, User1Id, SharedGroupRole.Viewer, 1);

        // Manually expire
        group.InviteLinkExpiresAt = DateTime.UtcNow.AddDays(-1);
        await context.SaveChangesAsync();

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sharingService.JoinViaInviteLinkAsync(code, User2Id));
    }

    [Fact]
    public async Task JoinViaInviteLinkAsync_InactiveLink_ThrowsException()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);
        var code = await sharingService.GenerateInviteLinkAsync(
            group.Id, User1Id, SharedGroupRole.Viewer);
        await sharingService.DeactivateInviteLinkAsync(group.Id, User1Id);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sharingService.JoinViaInviteLinkAsync(code, User2Id));
    }

    [Fact]
    public async Task JoinViaInviteLinkAsync_AlreadyMember_ThrowsException()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);
        var code = await sharingService.GenerateInviteLinkAsync(
            group.Id, User1Id, SharedGroupRole.Viewer);

        // User2 is already a member
        await AddMemberToGroup(group.Id, User2Id, SharedGroupRole.Viewer);

        // Act & Assert
        await Should.ThrowAsync<InvalidOperationException>(
            async () => await sharingService.JoinViaInviteLinkAsync(code, User2Id));
    }

    #endregion

    #region Email Content Tests

    [Fact]
    public async Task GetInvitationEmailContentAsync_GeneratesValidHtml()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);
        var invitation = await sharingService.InviteByEmailAsync(
            group.Id, User1Id, User2Email, SharedGroupRole.Contributor);

        // Act
        var content = await sharingService.GetInvitationEmailContentAsync(invitation);

        // Assert
        content.ShouldNotBeNullOrEmpty();
        content.ShouldContain("<!DOCTYPE html>");
        content.ShouldContain("</html>");
        content.ShouldContain(group.Name);
    }

    [Fact]
    public async Task GetInvitationEmailContentAsync_IncludesAllDetails()
    {
        // Arrange
        var group = await CreateGroupWithMember(User1Id, SharedGroupRole.Admin);
        group.Name = "Test Collaboration Group";
        await context.SaveChangesAsync();

        var invitation = await sharingService.InviteByEmailAsync(
            group.Id, User1Id, User2Email, SharedGroupRole.Editor, 7);

        // Act
        var content = await sharingService.GetInvitationEmailContentAsync(invitation);

        // Assert
        content.ShouldContain("Test Collaboration Group");
        content.ShouldContain("Editor");
        content.ShouldContain(User1Id); // Invited by
        content.ShouldContain("ACCEPT_URL"); // Placeholder for accept URL
        content.ShouldContain("DECLINE_URL"); // Placeholder for decline URL
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

    private void SetupUserManagerForUser(string userId, string email)
    {
        userManager.AddTestUser(userId, email);
    }

    #endregion
}
