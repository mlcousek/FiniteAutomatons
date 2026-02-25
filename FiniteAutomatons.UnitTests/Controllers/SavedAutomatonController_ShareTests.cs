using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.Security.Claims;
using System.Text.Json;

namespace FiniteAutomatons.UnitTests.Controllers;

public class SavedAutomatonController_ShareTests
{
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

    private class TestUserManager(ApplicationUser? user) : UserManager<ApplicationUser>(new FakeUserStore(), Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(), [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), services: null!, logger: new NullLogger<UserManager<ApplicationUser>>())
    {
        private readonly ApplicationUser? userToReturn = user;

        public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal) => Task.FromResult(userToReturn);
    }

    private class MockSavedAutomatonService : ISavedAutomatonService
    {
        public List<SavedAutomaton> Items = [];
        public Task<SavedAutomaton?> GetAsync(int id, string userId) => Task.FromResult(Items.FirstOrDefault(i => i.Id == id && i.UserId == userId));
        public Task<SavedAutomaton> SaveAsync(string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, int? groupId = null, string? layoutJson = null, string? thumbnailBase64 = null) => throw new NotImplementedException();
        public Task<List<SavedAutomaton>> ListForUserAsync(string userId, int? groupId = null) => throw new NotImplementedException();
        public Task DeleteAsync(int id, string userId) => throw new NotImplementedException();
        public Task<SavedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description) => throw new NotImplementedException();
        public Task<List<SavedAutomatonGroup>> ListGroupsForUserAsync(string userId) => Task.FromResult(new List<SavedAutomatonGroup>());
        public Task AddGroupMemberAsync(int groupId, string userId) => throw new NotImplementedException();
        public Task RemoveGroupMemberAsync(int groupId, string userId) => throw new NotImplementedException();
        public Task<List<SavedAutomatonGroupMember>> ListGroupMembersAsync(int groupId) => throw new NotImplementedException();
        public Task<bool> CanUserSaveToGroupAsync(int groupId, string userId) => throw new NotImplementedException();
        public Task<SavedAutomatonGroup?> GetGroupAsync(int groupId) => throw new NotImplementedException();
        public Task SetGroupSharingPolicyAsync(int groupId, bool membersCanShare) => throw new NotImplementedException();
        public Task<List<SavedAutomaton>> ListByGroupAsync(int groupId) => throw new NotImplementedException();
        public Task<SavedAutomatonGroup?> GetGroupAsync(int id, string userId) => throw new NotImplementedException();
        public Task DeleteGroupAsync(int id, string userId) => throw new NotImplementedException();
        public Task<SavedAutomatonGroupMember?> GetGroupMemberAsync(int groupId, string userId) => throw new NotImplementedException();
        public Task AssignAutomatonToGroupAsync(int automatonId, string userId, int? groupId) => throw new NotImplementedException();
        public Task RemoveAutomatonFromGroupAsync(int automatonId, string userId, int groupId) => throw new NotImplementedException();
    }

    private class MockSharedAutomatonService : ISharedAutomatonService
    {
        public List<SharedAutomaton> SavedItems = [];
        public Task<SharedAutomaton> SaveAsync(string userId, int groupId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, string? layoutJson = null, string? thumbnailBase64 = null)
        {
            var item = new SharedAutomaton
            {
                CreatedByUserId = userId,
                Name = name,
                Description = description,
                ContentJson = "{}",
                LayoutJson = string.IsNullOrWhiteSpace(layoutJson) ? null : layoutJson,
                ThumbnailBase64 = string.IsNullOrWhiteSpace(thumbnailBase64) ? null : thumbnailBase64
            };
            SavedItems.Add(item);
            return Task.FromResult(item);
        }
        public Task<List<SharedAutomatonGroup>> ListGroupsForUserAsync(string userId) => Task.FromResult(new List<SharedAutomatonGroup>());
        public Task<SharedAutomaton?> GetAsync(int id, string userId) => throw new NotImplementedException();
        public Task<List<SharedAutomaton>> ListForGroupAsync(int groupId, string userId) => throw new NotImplementedException();
        public Task<List<SharedAutomaton>> ListForUserAsync(string userId) => throw new NotImplementedException();
        public Task DeleteAsync(int id, string userId) => throw new NotImplementedException();
        public Task<SharedAutomaton> UpdateAsync(int id, string userId, string? name, string? description, AutomatonViewModel? model) => throw new NotImplementedException();
        public Task<SharedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description) => throw new NotImplementedException();
        public Task<SharedAutomatonGroup?> GetGroupAsync(int groupId, string userId) => throw new NotImplementedException();
        public Task DeleteGroupAsync(int groupId, string userId) => throw new NotImplementedException();
        public Task UpdateGroupAsync(int groupId, string userId, string? name, string? description) => throw new NotImplementedException();
        public Task<List<SharedAutomatonGroupMember>> ListGroupMembersAsync(int groupId, string userId) => throw new NotImplementedException();
        public Task RemoveMemberAsync(int groupId, string userId, string memberUserId) => throw new NotImplementedException();
        public Task UpdateMemberRoleAsync(int groupId, string userId, string memberUserId, SharedGroupRole newRole) => throw new NotImplementedException();
        public Task<bool> CanUserViewGroupAsync(int groupId, string userId) => throw new NotImplementedException();
        public Task<bool> CanUserAddToGroupAsync(int groupId, string userId) => throw new NotImplementedException();
        public Task<bool> CanUserEditInGroupAsync(int groupId, string userId) => throw new NotImplementedException();
        public Task<bool> CanUserManageMembersAsync(int groupId, string userId) => throw new NotImplementedException();
        public Task<SharedGroupRole?> GetUserRoleInGroupAsync(int groupId, string userId) => throw new NotImplementedException();
    }

    private static SavedAutomatonController BuildController(MockSavedAutomatonService savedSvc, MockSharedAutomatonService sharedSvc, ApplicationUser user)
    {
        var userManager = new TestUserManager(user);
        var tempDataSvc = new MockAutomatonTempDataService();
        var fileSvc = new MockAutomatonFileService();

        var controller = new SavedAutomatonController(savedSvc, sharedSvc, tempDataSvc, fileSvc, userManager);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)]));

        return controller;
    }

    [Fact]
    public async Task ShareToGroup_ValidAutomaton_CopiesToSharedServiceAndRedirects()
    {
        // Arrange
        var user = new ApplicationUser { Id = "u1" };
        var savedSvc = new MockSavedAutomatonService();
        var sharedSvc = new MockSharedAutomatonService();
        var controller = BuildController(savedSvc, sharedSvc, user);

        var payload = new { Type = AutomatonType.DFA };
        var entity = new SavedAutomaton
        {
            Id = 100,
            UserId = user.Id,
            Name = "My Automaton",
            Description = "A description",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.Structure
        };
        savedSvc.Items.Add(entity);

        // Act
        var result = await controller.ShareToGroup(100, 200) as RedirectToActionResult;

        // Assert
        result.ShouldNotBeNull();
        result.ActionName.ShouldBe("Index");
        result.ControllerName.ShouldBe("SharedAutomaton");
        result.RouteValues?["groupId"].ShouldBe(200);

        sharedSvc.SavedItems.Count.ShouldBe(1);
        sharedSvc.SavedItems[0].Name.ShouldBe("My Automaton");
        sharedSvc.SavedItems[0].CreatedByUserId.ShouldBe(user.Id);

        controller.TempData["CreateGroupResult"].ShouldBe("Automaton shared successfully!");
    }

    [Fact]
    public async Task ShareToGroup_WithState_PreservesState()
    {
        // Arrange
        var user = new ApplicationUser { Id = "u1" };
        var savedSvc = new MockSavedAutomatonService();
        var sharedSvc = new MockSharedAutomatonService();

        // Use a realish payload and exec state
        var payload = new { Type = AutomatonType.DFA, States = new List<object>(), Transitions = new List<object>() };
        var exec = new { Input = "abc", Position = 0 };

        var entity = new SavedAutomaton
        {
            Id = 101,
            UserId = user.Id,
            Name = "Stateful",
            ContentJson = JsonSerializer.Serialize(payload),
            ExecutionStateJson = JsonSerializer.Serialize(exec),
            SaveMode = AutomatonSaveMode.WithInput
        };
        savedSvc.Items.Add(entity);
        var controller = BuildController(savedSvc, sharedSvc, user);

        // Act
        await controller.ShareToGroup(101, 200);

        // Assert
        sharedSvc.SavedItems.Count.ShouldBe(1);
        sharedSvc.SavedItems[0].Name.ShouldBe("Stateful");
    }

    [Fact]
    public async Task ShareToGroup_NonExistent_ReturnsNotFound()
    {
        // Arrange
        var user = new ApplicationUser { Id = "u1" };
        var savedSvc = new MockSavedAutomatonService();
        var sharedSvc = new MockSharedAutomatonService();
        var controller = BuildController(savedSvc, sharedSvc, user);

        // Act
        var result = await controller.ShareToGroup(999, 200);

        // Assert
        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ShareToGroup_PreservesLayoutAndThumbnail()
    {
        // Arrange
        var user = new ApplicationUser { Id = "u1" };
        var savedSvc = new MockSavedAutomatonService();
        var sharedSvc = new MockSharedAutomatonService();
        var controller = BuildController(savedSvc, sharedSvc, user);

        var payload = new { Type = AutomatonType.DFA };
        var entity = new SavedAutomaton
        {
            Id = 200,
            UserId = user.Id,
            Name = "Thumbed",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.Structure,
            LayoutJson = "{\"0\":{\"x\":10,\"y\":20}}",
            ThumbnailBase64 = "iVBORw0KGgoAAAANSUhEUg..."
        };
        savedSvc.Items.Add(entity);

        // Act
        var result = await controller.ShareToGroup(200, 300) as RedirectToActionResult;

        // Assert
        result.ShouldNotBeNull();
        sharedSvc.SavedItems.Count.ShouldBe(1);
        sharedSvc.SavedItems[0].LayoutJson.ShouldBe(entity.LayoutJson);
        sharedSvc.SavedItems[0].ThumbnailBase64.ShouldBe(entity.ThumbnailBase64);
    }
}
