using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Data;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.Security.Claims;

namespace FiniteAutomatons.UnitTests.Controllers;

public class SharedAutomatonControllerTests : IDisposable
{
    private readonly ApplicationDbContext context;
    private readonly SharedAutomatonController controller;
    private readonly TestUserManager userManager;
    private readonly ISharedAutomatonService sharedService;
    private readonly ISharedAutomatonSharingService sharingService;
    private const string TestUserId = "test@user.com";
    private const string TestUser2Id = "test2@user.com";

    #region Test Infrastructure

    private sealed class TestTempDataProvider : ITempDataProvider
    {
        private readonly Dictionary<string, object?> data = [];
        public IDictionary<string, object?> LoadTempData(HttpContext context) => data;
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            data.Clear();
            foreach (var kv in values) data[kv.Key] = kv.Value;
        }
    }

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

    private class TestUserManager(ApplicationUser? user) : UserManager<ApplicationUser>(
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
        private readonly ApplicationUser? userToReturn = user;

        public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal)
        {
            return Task.FromResult(userToReturn);
        }
    }

    private class MockAutomatonTempDataService : IAutomatonTempDataService
    {
        public (bool Success, AutomatonViewModel? Model) TryGetCustomAutomaton(ITempDataDictionary tempData)
        {
            if (tempData.TryGetValue("CustomAutomaton", out var value) && value is string json)
            {
                try
                {
                    var model = System.Text.Json.JsonSerializer.Deserialize<AutomatonViewModel>(json);
                    return (true, model);
                }
                catch
                {
                    return (false, null);
                }
            }
            return (false, null);
        }

        public void StoreCustomAutomaton(ITempDataDictionary tempData, AutomatonViewModel model)
        {
            var modelJson = System.Text.Json.JsonSerializer.Serialize(model);
            tempData["CustomAutomaton"] = modelJson;
        }

        public void StoreErrorMessage(ITempDataDictionary tempData, string errorMessage)
        {
            tempData["ErrorMessage"] = errorMessage;
        }

        public void StoreConversionMessage(ITempDataDictionary tempData, string message)
        {
            tempData["ConversionMessage"] = message;
        }
    }

    private class MockInvitationNotificationService : IInvitationNotificationService
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
            return Task.CompletedTask;
        }
    }

    #endregion

    public SharedAutomatonControllerTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        context = new ApplicationDbContext(options);

        var testUser = new ApplicationUser { Id = TestUserId, UserName = TestUserId, Email = TestUserId, EnableInvitationNotifications = true };
        userManager = new TestUserManager(testUser);

        sharedService = new SharedAutomatonService(context, NullLogger<SharedAutomatonService>.Instance);
        sharingService = new SharedAutomatonSharingService(
            context,
            sharedService,
            userManager,
            NullLogger<SharedAutomatonSharingService>.Instance);

        var tempDataService = new MockAutomatonTempDataService();
        var invitationService = new MockInvitationNotificationService();

        controller = new SharedAutomatonController(
            sharedService,
            sharingService,
            tempDataService,
            userManager,
            context,
            invitationService,
            NullLogger<SharedAutomatonController>.Instance);

        SetupControllerContext();
    }

    private void SetupControllerContext()
    {
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, TestUserId) };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));

        controller.ControllerContext = new ControllerContext
        {
            HttpContext = httpContext
        };

        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
    }

    public void Dispose()
    {
        context.Database.EnsureDeleted();
        context.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Basic CRUD Tests

    [Fact]
    public async Task Index_ReturnsViewWithAutomatons()
    {
        // Arrange
        var group = await CreateTestGroup(TestUserId, SharedGroupRole.Owner);
        await SaveTestAutomaton(group.Id, "Test Automaton");

        // Act
        var result = await controller.Index(group.Id);

        // Assert
        var viewResult = result.ShouldBeOfType<ViewResult>();
        var model = viewResult.Model.ShouldBeOfType<List<SharedAutomaton>>();
        model.Count.ShouldBe(1);
        model[0].Name.ShouldBe("Test Automaton");
    }

    [Fact]
    public async Task CreateGroup_ValidData_RedirectsToIndex()
    {
        // Act
        var result = await controller.CreateGroup("My Group", "Test Description");

        // Assert
        var redirectResult = result.ShouldBeOfType<RedirectToActionResult>();
        redirectResult.ActionName.ShouldBe(nameof(controller.Index));

        controller.TempData["CreateGroupResult"].ShouldNotBeNull();
        controller.TempData["CreateGroupSuccess"].ShouldBe("1");
    }

    [Fact]
    public async Task DeleteGroup_AsOwner_Success()
    {
        // Arrange
        var group = await CreateTestGroup(TestUserId, SharedGroupRole.Owner);

        // Act
        var result = await controller.DeleteGroup(group.Id);

        // Assert
        var redirectResult = result.ShouldBeOfType<RedirectToActionResult>();
        redirectResult.ActionName.ShouldBe(nameof(controller.Index));

        var deleted = await context.SharedAutomatonGroups.FindAsync(group.Id);
        deleted.ShouldBeNull();
    }

    [Fact]
    public async Task Save_ValidData_CreatesAutomaton()
    {
        // Arrange
        var group = await CreateTestGroup(TestUserId, SharedGroupRole.Contributor);
        var model = CreateTestAutomatonViewModel();

        // Store model in TempData as JSON (simulating how the real service works)
        var modelJson = System.Text.Json.JsonSerializer.Serialize(model);
        controller.TempData["CustomAutomaton"] = modelJson;

        // Act
        var result = await controller.Save(group.Id, "Test Automaton", "Description", false);

        // Assert
        var redirectResult = result.ShouldBeOfType<RedirectToActionResult>();

        var automatons = await context.SharedAutomatons.ToListAsync();
        automatons.Count.ShouldBe(1);
        automatons[0].Name.ShouldBe("Test Automaton");
    }

    [Fact]
    public async Task Load_ValidId_LoadsToEditor()
    {
        // Arrange
        var group = await CreateTestGroup(TestUserId, SharedGroupRole.Contributor);
        var automaton = await SaveTestAutomaton(group.Id, "Test");

        // Act
        var result = await controller.Load(automaton.Id, "structure");

        // Assert
        var redirectResult = result.ShouldBeOfType<RedirectToActionResult>();
        redirectResult.ActionName.ShouldBe("Index");
        redirectResult.ControllerName.ShouldBe("Home");

        controller.TempData["LoadMessage"].ShouldNotBeNull();
    }

    [Fact]
    public async Task Delete_AsEditor_Success()
    {
        // Arrange
        var group = await CreateTestGroup(TestUserId, SharedGroupRole.Editor);
        var automaton = await SaveTestAutomaton(group.Id, "Test");

        // Act
        var result = await controller.Delete(automaton.Id);

        // Assert
        result.ShouldBeOfType<RedirectToActionResult>();

        var deleted = await context.SharedAutomatons.FindAsync(automaton.Id);
        deleted.ShouldBeNull();
    }

    [Fact]
    public async Task UpdateMemberRole_ValidChange_Success()
    {
        // Arrange
        var group = await CreateTestGroup(TestUserId, SharedGroupRole.Admin);
        await AddMemberToGroup(group.Id, TestUser2Id, SharedGroupRole.Viewer);

        // Act
        var result = await controller.UpdateMemberRole(group.Id, TestUser2Id, SharedGroupRole.Editor);

        // Assert
        result.ShouldBeOfType<RedirectToActionResult>();

        var member = await context.SharedAutomatonGroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == group.Id && m.UserId == TestUser2Id);
        member.ShouldNotBeNull();
        member.Role.ShouldBe(SharedGroupRole.Editor);
    }

    [Fact]
    public async Task RemoveMember_AsAdmin_Success()
    {
        // Arrange
        var group = await CreateTestGroup(TestUserId, SharedGroupRole.Admin);
        await AddMemberToGroup(group.Id, TestUser2Id, SharedGroupRole.Viewer);

        // Act
        var result = await controller.RemoveMember(group.Id, TestUser2Id);

        // Assert
        result.ShouldBeOfType<RedirectToActionResult>();

        var member = await context.SharedAutomatonGroupMembers
            .FirstOrDefaultAsync(m => m.GroupId == group.Id && m.UserId == TestUser2Id);
        member.ShouldBeNull();
    }

    #endregion

    #region Authorization Tests

    [Fact]
    public async Task Index_Unauthenticated_Challenges()
    {
        // Arrange
        var unauthController = new SharedAutomatonController(
            sharedService,
            sharingService,
            new MockAutomatonTempDataService(),
            new TestUserManager(null),
            context,
            new MockInvitationNotificationService(),
            NullLogger<SharedAutomatonController>.Instance);

        unauthController.ControllerContext = new ControllerContext
        {
            HttpContext = new DefaultHttpContext()
        };

        // Act
        var result = await unauthController.Index(null);

        // Assert
        result.ShouldBeOfType<ChallengeResult>();
    }

    [Fact]
    public async Task DeleteGroup_NotOwner_ReturnsForbid()
    {
        // Arrange
        var group = await CreateTestGroup(TestUser2Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, TestUserId, SharedGroupRole.Admin);

        // Act
        var result = await controller.DeleteGroup(group.Id);

        // Assert
        var redirectResult = result.ShouldBeOfType<RedirectToActionResult>();
        controller.TempData["CreateGroupSuccess"].ShouldBe("0");
    }

    [Fact]
    public async Task Save_NoPermission_ReturnsUnauthorized()
    {
        // Arrange
        var group = await CreateTestGroup(TestUser2Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, TestUserId, SharedGroupRole.Viewer);

        var model = CreateTestAutomatonViewModel();
        var tempDataService = new MockAutomatonTempDataService();
        tempDataService.StoreCustomAutomaton(controller.TempData, model);

        // Act
        var result = await controller.Save(group.Id, "Test", null, false);

        // Assert
        var redirectResult = result.ShouldBeOfType<RedirectToActionResult>();
        controller.TempData["SaveError"].ShouldNotBeNull();
    }

    [Fact]
    public async Task Delete_NoPermission_ReturnsUnauthorized()
    {
        // Arrange
        var group = await CreateTestGroup(TestUser2Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, TestUserId, SharedGroupRole.Viewer);
        var automaton = await SaveTestAutomaton(group.Id, "Test", TestUser2Id);

        // Act
        var result = await controller.Delete(automaton.Id);

        // Assert
        var redirectResult = result.ShouldBeOfType<RedirectToActionResult>();
        controller.TempData["CreateGroupSuccess"].ShouldBe("0");
    }

    [Fact]
    public async Task ManageMembers_NotAdmin_ReturnsForbid()
    {
        // Arrange
        var group = await CreateTestGroup(TestUser2Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, TestUserId, SharedGroupRole.Contributor);

        // Act
        var result = await controller.ManageMembers(group.Id);

        // Assert
        var redirectResult = result.ShouldBeOfType<RedirectToActionResult>();
        controller.TempData["Error"].ShouldNotBeNull();
    }

    [Fact]
    public async Task UpdateMemberRole_NoPermission_ReturnsForbid()
    {
        // Arrange
        var group = await CreateTestGroup(TestUser2Id, SharedGroupRole.Owner);
        await AddMemberToGroup(group.Id, TestUserId, SharedGroupRole.Contributor);

        // Act
        var result = await controller.UpdateMemberRole(group.Id, TestUser2Id, SharedGroupRole.Admin);

        // Assert
        result.ShouldBeOfType<RedirectToActionResult>();
        controller.TempData["MemberSuccess"].ShouldBe("0");
    }

    #endregion

    #region Sharing Tests

    [Fact]
    public async Task InviteByEmail_ValidEmail_SendsInvitation()
    {
        // Arrange
        var group = await CreateTestGroup(TestUserId, SharedGroupRole.Admin);

        // Act
        var result = await controller.InviteByEmail(group.Id, "invited@test.com", SharedGroupRole.Contributor);

        // Assert
        result.ShouldBeOfType<RedirectToActionResult>();
        controller.TempData["MemberSuccess"].ShouldBe("1");

        var invitation = await context.SharedAutomatonGroupInvitations
            .FirstOrDefaultAsync(i => i.Email == "invited@test.com");
        invitation.ShouldNotBeNull();
    }

    [Fact]
    public async Task AcceptInvitation_ValidToken_InvitationExists()
    {
        // Arrange  
        var group = await CreateTestGroup(TestUser2Id, SharedGroupRole.Admin);
        var invitation = await sharingService.InviteByEmailAsync(
            group.Id, TestUser2Id, "newuser@test.com", SharedGroupRole.Contributor);

        // Act - Just verify the action processes the token
        var getResult = await controller.AcceptInvitation(invitation.Token);

        // Assert - View is shown with invitation details
        var viewResult = getResult.ShouldBeOfType<ViewResult>();
        var invitationInView = viewResult.ViewData["Invitation"];
        invitationInView.ShouldNotBeNull();
    }

    [Fact]
    public async Task GenerateInviteLink_AsAdmin_ReturnsLink()
    {
        // Arrange
        var group = await CreateTestGroup(TestUserId, SharedGroupRole.Admin);

        // Act
        var result = await controller.GenerateInviteLink(group.Id, SharedGroupRole.Viewer, 30);

        // Assert
        result.ShouldBeOfType<RedirectToActionResult>();

        // Verify the invite code was created
        var updatedGroup = await context.SharedAutomatonGroups.FindAsync(group.Id);
        updatedGroup.ShouldNotBeNull();
        updatedGroup.InviteCode.ShouldNotBeNull();
        updatedGroup.InviteCode.Length.ShouldBe(8);
        updatedGroup.IsInviteLinkActive.ShouldBeTrue();
        updatedGroup.DefaultRoleForInvite.ShouldBe(SharedGroupRole.Viewer);
    }

    [Fact]
    public async Task JoinViaLink_ValidCode_RedirectsToIndex()
    {
        // Arrange
        var group = await CreateTestGroup(TestUser2Id, SharedGroupRole.Owner);
        var code = await sharingService.GenerateInviteLinkAsync(
            group.Id, TestUser2Id, SharedGroupRole.Contributor, 30);

        // Setup authenticated user context
        var httpContext = new DefaultHttpContext();
        var claims = new List<Claim> { new(ClaimTypes.NameIdentifier, TestUserId) };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(claims, "TestAuth"));
        httpContext.Request.Scheme = "https";
        httpContext.Request.Host = new HostString("localhost");

        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };

        // Act
        var result = await controller.JoinViaLink(code);

        // Assert
        result.ShouldBeOfType<ViewResult>();
    }

    #endregion

    #region Helper Methods

    private async Task<SharedAutomatonGroup> CreateTestGroup(string userId, SharedGroupRole role)
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

    private async Task<SharedAutomaton> SaveTestAutomaton(int groupId, string name, string? createdBy = null)
    {
        var model = CreateTestAutomatonViewModel();
        return await sharedService.SaveAsync(createdBy ?? TestUserId, groupId, name, "Test", model);
    }

    private static AutomatonViewModel CreateTestAutomatonViewModel()
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
