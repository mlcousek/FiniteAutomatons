using FiniteAutomatons.Areas.Identity.Pages.Account.Manage;
using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.Security.Claims;

namespace FiniteAutomatons.UnitTests.Areas.Identity.Pages.Account.Manage;

public class NotificationsPageModelTests
{
    private readonly TestUserManager userManager;
    private readonly MockInvitationNotificationService notificationService;
    private readonly NotificationsModel pageModel;

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
        logger: NullLogger<UserManager<ApplicationUser>>.Instance)
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

    private class MockInvitationNotificationService : IInvitationNotificationService
    {
        private readonly Dictionary<string, bool> preferences = [];

        public void SetPreference(string userId, bool enabled)
        {
            preferences[userId] = enabled;
        }

        public Task<List<SharedAutomatonGroupInvitation>> GetPendingInvitationsForUserAsync(string userId, string email)
        {
            return Task.FromResult(new List<SharedAutomatonGroupInvitation>());
        }

        public Task<bool> HasInvitationNotificationsEnabledAsync(string userId)
        {
            return Task.FromResult(preferences.GetValueOrDefault(userId, true));
        }

        public Task SetInvitationNotificationsAsync(string userId, bool enabled)
        {
            SetPreference(userId, enabled);
            return Task.CompletedTask;
        }
    }

    public NotificationsPageModelTests()
    {
        userManager = new TestUserManager();
        notificationService = new MockInvitationNotificationService();
        pageModel = new NotificationsModel(userManager, notificationService);
    }

    private void SetupPageContext(string userId = "test@user.com")
    {
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, userId) };
        var identity = new ClaimsIdentity(claims, "Test");
        var principal = new ClaimsPrincipal(identity);

        pageModel.PageContext = new PageContext
        {
            HttpContext = new Microsoft.AspNetCore.Http.DefaultHttpContext { User = principal }
        };
    }

    #region OnGet Tests

    [Fact]
    public async Task OnGetAsync_WithValidUser_LoadsNotificationPreference()
    {
        // Arrange
        const string userId = "test@user.com";
        _ = new ApplicationUser
        {
            Id = userId,
            Email = userId,
            EnableInvitationNotifications = true
        };

        userManager.AddTestUser(userId, userId);
        SetupPageContext(userId);

        // Act
        var result = await pageModel.OnGetAsync();

        // Assert
        result.ShouldBeOfType<PageResult>();
        pageModel.EnableInvitationNotifications.ShouldBeTrue();
    }

    [Fact]
    public async Task OnGetAsync_WithDisabledNotifications_LoadsCorrectPreference()
    {
        // Arrange
        const string userId = "test@user.com";
        userManager.AddTestUser(userId, userId, enableNotifications: false);
        SetupPageContext(userId);

        // Act
        var result = await pageModel.OnGetAsync();

        // Assert
        result.ShouldBeOfType<PageResult>();
        pageModel.EnableInvitationNotifications.ShouldBeFalse();
    }

    [Fact]
    public async Task OnGetAsync_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        SetupPageContext();

        // Act
        var result = await pageModel.OnGetAsync();

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    #endregion

    #region OnPost Tests

    [Fact]
    public async Task OnPostAsync_WithValidUser_EnablesNotifications()
    {
        // Arrange
        const string userId = "test@user.com";
        userManager.AddTestUser(userId, userId, enableNotifications: false);
        SetupPageContext(userId);
        pageModel.EnableInvitationNotifications = true;

        // Act
        var result = await pageModel.OnPostAsync();

        // Assert
        result.ShouldBeOfType<RedirectToPageResult>();
        pageModel.StatusMessage.ShouldNotBeNullOrEmpty();
        pageModel.StatusMessage.ShouldContain("updated successfully");
    }

    [Fact]
    public async Task OnPostAsync_WithValidUser_DisablesNotifications()
    {
        // Arrange
        const string userId = "test@user.com";
        userManager.AddTestUser(userId, userId, enableNotifications: true);
        SetupPageContext(userId);
        pageModel.EnableInvitationNotifications = false;

        // Act
        var result = await pageModel.OnPostAsync();

        // Assert
        result.ShouldBeOfType<RedirectToPageResult>();
        pageModel.StatusMessage.ShouldNotBeNullOrEmpty();
        pageModel.StatusMessage.ShouldContain("updated successfully");
    }

    [Fact]
    public async Task OnPostAsync_UserNotFound_ReturnsNotFound()
    {
        // Arrange
        SetupPageContext();
        pageModel.EnableInvitationNotifications = true;

        // Act
        var result = await pageModel.OnPostAsync();

        // Assert
        result.ShouldBeOfType<NotFoundObjectResult>();
    }

    [Fact]
    public async Task OnPostAsync_ServiceThrowsException_ShowsErrorMessage()
    {
        // Arrange
        const string userId = "test@user.com";
        userManager.AddTestUser(userId, userId);
        SetupPageContext(userId);
        pageModel.EnableInvitationNotifications = true;

        // Create a service that throws
        var faultyService = new FaultyNotificationService();
        var faultyPageModel = new NotificationsModel(userManager, faultyService)
        {
            PageContext = pageModel.PageContext
        };

        // Act
        var result = await faultyPageModel.OnPostAsync();

        // Assert
        result.ShouldBeOfType<RedirectToPageResult>();
        faultyPageModel.StatusMessage.ShouldNotBeNullOrEmpty();
        faultyPageModel.StatusMessage!.ShouldContain("Error");
    }

    [Fact]
    public async Task OnPostAsync_MultipleToggles_PersistsCorrectly()
    {
        // Arrange
        const string userId = "test@user.com";
        userManager.AddTestUser(userId, userId, enableNotifications: true);
        SetupPageContext(userId);

        // Act - Disable
        pageModel.EnableInvitationNotifications = false;
        var result1 = await pageModel.OnPostAsync();
        var disabledPref = await notificationService.HasInvitationNotificationsEnabledAsync(userId);

        // Act - Enable
        pageModel.EnableInvitationNotifications = true;
        var result2 = await pageModel.OnPostAsync();
        var enabledPref = await notificationService.HasInvitationNotificationsEnabledAsync(userId);

        // Assert
        result1.ShouldBeOfType<RedirectToPageResult>();
        result2.ShouldBeOfType<RedirectToPageResult>();
        disabledPref.ShouldBeFalse();
        enabledPref.ShouldBeTrue();
    }

    #endregion

    #region Helper Classes

    private class FaultyNotificationService : IInvitationNotificationService
    {
        public Task<List<SharedAutomatonGroupInvitation>> GetPendingInvitationsForUserAsync(string userId, string email)
        {
            return Task.FromResult(new List<SharedAutomatonGroupInvitation>());
        }

        public Task<bool> HasInvitationNotificationsEnabledAsync(string userId)
        {
            return Task.FromResult(true);
        }

        public Task SetInvitationNotificationsAsync(string userId, bool enabled)
        {
            throw new InvalidOperationException("Service error");
        }
    }

    #endregion
}

