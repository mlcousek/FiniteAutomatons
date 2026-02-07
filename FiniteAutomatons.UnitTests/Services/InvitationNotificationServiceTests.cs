using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Data;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

public class InvitationNotificationServiceTests : IDisposable
{
    private readonly ApplicationDbContext context;
    private readonly InvitationNotificationService notificationService;
    private readonly TestUserManager userManager;

    private const string TestUserId = "test@user.com";
    private const string TestUserEmail = "test@user.com";

    private sealed class FakeUserStore : IUserStore<ApplicationUser>
    {
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public void Dispose() { }
        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.UserName?.ToUpperInvariant());
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult<string>(user.Id);
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.UserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }
        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
    }

    private class TestUserManager() : UserManager<ApplicationUser>(
        new FakeUserStore(),
        Microsoft.Extensions.Options.Options.Create(new IdentityOptions()),
        new PasswordHasher<ApplicationUser>(),
        [],
        [],
        new UpperInvariantLookupNormalizer(),
        new IdentityErrorDescriber(),
        services: null!,
        logger: new NullLogger<UserManager<ApplicationUser>>())
    {
        private readonly Dictionary<string, ApplicationUser> users = [];

        public void AddTestUser(string userId, string email, bool enableNotifications = true)
        {
            users[userId] = new ApplicationUser
            {
                Id = userId,
                Email = email,
                UserName = email,
                EnableInvitationNotifications = enableNotifications
            };
        }

        public override Task<ApplicationUser?> FindByIdAsync(string userId)
        {
            users.TryGetValue(userId, out var user);
            return Task.FromResult(user);
        }

        public override Task<IdentityResult> UpdateAsync(ApplicationUser user)
        {
            if (users.ContainsKey(user.Id))
            {
                users[user.Id] = user;
            }
            return Task.FromResult(IdentityResult.Success);
        }
    }

    public InvitationNotificationServiceTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        context = new ApplicationDbContext(options);
        userManager = new TestUserManager();

        notificationService = new InvitationNotificationService(context, userManager);
    }

    public void Dispose()
    {
        context.Database.EnsureDeleted();
        context.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Get Pending Invitations Tests

    [Fact]
    public async Task GetPendingInvitationsForUserAsync_WithPendingInvitations_ReturnsList()
    {
        // Arrange
        userManager.AddTestUser(TestUserId, TestUserEmail);
        var group = await CreateTestGroup();

        var invitations = new List<SharedAutomatonGroupInvitation>
        {
            new()
            {
                GroupId = group.Id,
                Email = TestUserEmail.ToLowerInvariant(),
                Role = SharedGroupRole.Contributor,
                InvitedByUserId = "admin@test.com",
                Token = "token1",
                CreatedAt = DateTime.UtcNow,
                Status = InvitationStatus.Pending
            },
            new()
            {
                GroupId = group.Id,
                Email = TestUserEmail.ToLowerInvariant(),
                Role = SharedGroupRole.Editor,
                InvitedByUserId = "admin@test.com",
                Token = "token2",
                CreatedAt = DateTime.UtcNow,
                Status = InvitationStatus.Pending
            }
        };

        context.SharedAutomatonGroupInvitations.AddRange(invitations);
        await context.SaveChangesAsync();

        // Act
        var result = await notificationService.GetPendingInvitationsForUserAsync(TestUserId, TestUserEmail);

        // Assert
        result.Count.ShouldBe(2);
        result.All(i => i.Status == InvitationStatus.Pending).ShouldBeTrue();
        result.All(i => i.Email.Equals(TestUserEmail, StringComparison.InvariantCultureIgnoreCase)).ShouldBeTrue();
    }

    [Fact]
    public async Task GetPendingInvitationsForUserAsync_NoInvitations_ReturnsEmptyList()
    {
        // Arrange
        userManager.AddTestUser(TestUserId, TestUserEmail);

        // Act
        var result = await notificationService.GetPendingInvitationsForUserAsync(TestUserId, TestUserEmail);

        // Assert
        result.Count.ShouldBe(0);
        result.ShouldBeEmpty();
    }

    [Fact]
    public async Task GetPendingInvitationsForUserAsync_FiltersByEmail()
    {
        // Arrange
        userManager.AddTestUser(TestUserId, TestUserEmail);
        var group = await CreateTestGroup();

        var invitations = new List<SharedAutomatonGroupInvitation>
        {
            new()
            {
                GroupId = group.Id,
                Email = "test1@test.com",
                Role = SharedGroupRole.Contributor,
                InvitedByUserId = "admin@test.com",
                Token = "token1",
                CreatedAt = DateTime.UtcNow,
                Status = InvitationStatus.Pending
            },
            new()
            {
                GroupId = group.Id,
                Email = "test2@test.com",
                Role = SharedGroupRole.Editor,
                InvitedByUserId = "admin@test.com",
                Token = "token2",
                CreatedAt = DateTime.UtcNow,
                Status = InvitationStatus.Pending
            }
        };

        context.SharedAutomatonGroupInvitations.AddRange(invitations);
        await context.SaveChangesAsync();

        // Act
        var result = await notificationService.GetPendingInvitationsForUserAsync(TestUserId, "test1@test.com");

        // Assert
        result.Count.ShouldBe(1);
        result[0].Email.ShouldBe("test1@test.com");
    }

    [Fact]
    public async Task GetPendingInvitationsForUserAsync_ExcludesNonPendingInvitations()
    {
        // Arrange
        userManager.AddTestUser(TestUserId, TestUserEmail);
        var group = await CreateTestGroup();

        var invitations = new List<SharedAutomatonGroupInvitation>
        {
            new()
            {
                GroupId = group.Id,
                Email = TestUserEmail.ToLowerInvariant(),
                Role = SharedGroupRole.Contributor,
                InvitedByUserId = "admin@test.com",
                Token = "token1",
                CreatedAt = DateTime.UtcNow,
                Status = InvitationStatus.Pending
            },
            new()
            {
                GroupId = group.Id,
                Email = TestUserEmail.ToLowerInvariant(),
                Role = SharedGroupRole.Editor,
                InvitedByUserId = "admin@test.com",
                Token = "token2",
                CreatedAt = DateTime.UtcNow,
                Status = InvitationStatus.Accepted
            },
            new()
            {
                GroupId = group.Id,
                Email = TestUserEmail.ToLowerInvariant(),
                Role = SharedGroupRole.Viewer,
                InvitedByUserId = "admin@test.com",
                Token = "token3",
                CreatedAt = DateTime.UtcNow,
                Status = InvitationStatus.Declined
            }
        };

        context.SharedAutomatonGroupInvitations.AddRange(invitations);
        await context.SaveChangesAsync();

        // Act
        var result = await notificationService.GetPendingInvitationsForUserAsync(TestUserId, TestUserEmail);

        // Assert
        result.Count.ShouldBe(1);
        result[0].Status.ShouldBe(InvitationStatus.Pending);
    }

    [Fact]
    public async Task GetPendingInvitationsForUserAsync_OrdersByNewest()
    {
        // Arrange
        userManager.AddTestUser(TestUserId, TestUserEmail);
        var group = await CreateTestGroup();

        var olderDate = DateTime.UtcNow.AddDays(-5);
        var newerDate = DateTime.UtcNow;

        var invitations = new List<SharedAutomatonGroupInvitation>
        {
            new()
            {
                GroupId = group.Id,
                Email = TestUserEmail.ToLowerInvariant(),
                Role = SharedGroupRole.Contributor,
                InvitedByUserId = "admin@test.com",
                Token = "token1",
                CreatedAt = olderDate,
                Status = InvitationStatus.Pending
            },
            new()
            {
                GroupId = group.Id,
                Email = TestUserEmail.ToLowerInvariant(),
                Role = SharedGroupRole.Editor,
                InvitedByUserId = "admin@test.com",
                Token = "token2",
                CreatedAt = newerDate,
                Status = InvitationStatus.Pending
            }
        };

        context.SharedAutomatonGroupInvitations.AddRange(invitations);
        await context.SaveChangesAsync();

        // Act
        var result = await notificationService.GetPendingInvitationsForUserAsync(TestUserId, TestUserEmail);

        // Assert
        result.Count.ShouldBe(2);
        result[0].CreatedAt.ShouldBeGreaterThan(result[1].CreatedAt);
    }

    #endregion

    #region Notification Preference Tests

    [Fact]
    public async Task HasInvitationNotificationsEnabledAsync_Enabled_ReturnsTrue()
    {
        // Arrange
        userManager.AddTestUser(TestUserId, TestUserEmail, enableNotifications: true);

        // Act
        var result = await notificationService.HasInvitationNotificationsEnabledAsync(TestUserId);

        // Assert
        result.ShouldBeTrue();
    }

    [Fact]
    public async Task HasInvitationNotificationsEnabledAsync_Disabled_ReturnsFalse()
    {
        // Arrange
        userManager.AddTestUser(TestUserId, TestUserEmail, enableNotifications: false);

        // Act
        var result = await notificationService.HasInvitationNotificationsEnabledAsync(TestUserId);

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task HasInvitationNotificationsEnabledAsync_UserNotFound_ReturnsFalse()
    {
        // Act
        var result = await notificationService.HasInvitationNotificationsEnabledAsync("nonexistent@test.com");

        // Assert
        result.ShouldBeFalse();
    }

    [Fact]
    public async Task HasInvitationNotificationsEnabledAsync_WithNullUserId_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await notificationService.HasInvitationNotificationsEnabledAsync(null!));
    }

    #endregion

    #region Set Notification Preference Tests

    [Fact]
    public async Task SetInvitationNotificationsAsync_EnableNotifications_UpdatesUser()
    {
        // Arrange
        userManager.AddTestUser(TestUserId, TestUserEmail, enableNotifications: false);

        // Act
        await notificationService.SetInvitationNotificationsAsync(TestUserId, true);

        // Assert
        var user = await userManager.FindByIdAsync(TestUserId);
        user.ShouldNotBeNull();
        user.EnableInvitationNotifications.ShouldBeTrue();
    }

    [Fact]
    public async Task SetInvitationNotificationsAsync_DisableNotifications_UpdatesUser()
    {
        // Arrange
        userManager.AddTestUser(TestUserId, TestUserEmail, enableNotifications: true);

        // Act
        await notificationService.SetInvitationNotificationsAsync(TestUserId, false);

        // Assert
        var user = await userManager.FindByIdAsync(TestUserId);
        user.ShouldNotBeNull();
        user.EnableInvitationNotifications.ShouldBeFalse();
    }

    [Fact]
    public async Task SetInvitationNotificationsAsync_WithNullUserId_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await notificationService.SetInvitationNotificationsAsync(null!, true));
    }

    [Fact]
    public async Task SetInvitationNotificationsAsync_UserNotFound_ThrowsArgumentNullException()
    {
        // Act & Assert
        await Should.ThrowAsync<ArgumentNullException>(
            async () => await notificationService.SetInvitationNotificationsAsync("nonexistent@test.com", true));
    }

    [Fact]
    public async Task SetInvitationNotificationsAsync_MultipleToggles_UpdatesCorrectly()
    {
        // Arrange
        userManager.AddTestUser(TestUserId, TestUserEmail, enableNotifications: true);

        // Act - Toggle multiple times
        await notificationService.SetInvitationNotificationsAsync(TestUserId, false);
        var disabled = await userManager.FindByIdAsync(TestUserId);
        disabled!.EnableInvitationNotifications.ShouldBeFalse();

        await notificationService.SetInvitationNotificationsAsync(TestUserId, true);
        var enabled = await userManager.FindByIdAsync(TestUserId);
        enabled!.EnableInvitationNotifications.ShouldBeTrue();

        // Assert
        enabled.EnableInvitationNotifications.ShouldBeTrue();
    }

    #endregion

    #region Helper Methods

    private async Task<SharedAutomatonGroup> CreateTestGroup()
    {
        var group = new SharedAutomatonGroup
        {
            UserId = "admin@test.com",
            Name = $"Group_{Guid.NewGuid():N}",
            Description = "Test group",
            CreatedAt = DateTime.UtcNow
        };

        context.SharedAutomatonGroups.Add(group);
        await context.SaveChangesAsync();
        return group;
    }

    #endregion
}
