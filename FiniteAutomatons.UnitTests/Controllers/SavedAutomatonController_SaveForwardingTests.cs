using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.Security.Claims;

namespace FiniteAutomatons.UnitTests.Controllers;

public class SavedAutomatonController_SaveForwardingTests
{
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

    private class TestUserManager(ApplicationUser? user) : UserManager<ApplicationUser>(new FakeUserStore(), Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), new PasswordHasher<ApplicationUser>(), Array.Empty<IUserValidator<ApplicationUser>>(), Array.Empty<IPasswordValidator<ApplicationUser>>(), new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), services: null!, logger: new NullLogger<UserManager<ApplicationUser>>())
    {
        private readonly ApplicationUser? _user = user;
        public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal) => Task.FromResult(_user);
    }

    private class RecordingSavedAutomatonService : ISavedAutomatonService
    {
        public string? LastLayoutJson;
        public string? LastThumbnailBase64;

        public Task<SavedAutomaton> SaveAsync(string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, int? groupId = null, string? layoutJson = null, string? thumbnailBase64 = null)
        {
            LastLayoutJson = layoutJson;
            LastThumbnailBase64 = thumbnailBase64;
            var e = new SavedAutomaton { Id = 1, UserId = userId, Name = name, ContentJson = "{}", SaveMode = AutomatonSaveMode.Structure, LayoutJson = layoutJson, ThumbnailBase64 = thumbnailBase64 };
            return Task.FromResult(e);
        }

        // Other interface members - minimal stubs
        public Task DeleteAsync(int id, string userId) => Task.CompletedTask;
        public Task<List<SavedAutomaton>> ListForUserAsync(string userId, int? groupId = null) => Task.FromResult(new List<SavedAutomaton>());
        public Task<SavedAutomaton?> GetAsync(int id, string userId) => Task.FromResult<SavedAutomaton?>(null);
        public Task<SavedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description) => throw new NotImplementedException();
        public Task<List<SavedAutomatonGroup>> ListGroupsForUserAsync(string userId) => Task.FromResult(new List<SavedAutomatonGroup>());
        public Task AddGroupMemberAsync(int groupId, string userId) => Task.CompletedTask;
        public Task RemoveGroupMemberAsync(int groupId, string userId) => Task.CompletedTask;
        public Task<List<SavedAutomatonGroupMember>> ListGroupMembersAsync(int groupId) => Task.FromResult(new List<SavedAutomatonGroupMember>());
        public Task<bool> CanUserSaveToGroupAsync(int groupId, string userId) => Task.FromResult(true);
        public Task<SavedAutomatonGroup?> GetGroupAsync(int groupId) => Task.FromResult<SavedAutomatonGroup?>(null);
        public Task SetGroupSharingPolicyAsync(int groupId, bool membersCanShare) => Task.CompletedTask;
        public Task<List<SavedAutomaton>> ListByGroupAsync(int groupId) => Task.FromResult(new List<SavedAutomaton>());
        public Task<SavedAutomatonGroup?> GetGroupAsync(int id, string userId) => Task.FromResult<SavedAutomatonGroup?>(null);
        public Task DeleteGroupAsync(int id, string userId) => Task.CompletedTask;
        public Task<SavedAutomatonGroupMember?> GetGroupMemberAsync(int groupId, string userId) => Task.FromResult<SavedAutomatonGroupMember?>(null);
        public Task AssignAutomatonToGroupAsync(int automatonId, string userId, int? groupId) => Task.CompletedTask;
        public Task RemoveAutomatonFromGroupAsync(int automatonId, string userId, int groupId) => Task.CompletedTask;
    }

    private class SimpleSharedAutomatonService : ISharedAutomatonService
    {
        public Task<SharedAutomaton> SaveAsync(string userId, int groupId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, string? layoutJson = null, string? thumbnailBase64 = null)
            => Task.FromResult(new SharedAutomaton { CreatedByUserId = userId, Name = name, Description = description, ContentJson = "{}", LayoutJson = layoutJson, ThumbnailBase64 = thumbnailBase64 });

        public Task<List<SharedAutomatonGroup>> ListGroupsForUserAsync(string userId) => Task.FromResult(new List<SharedAutomatonGroup>());
        public Task<SharedAutomaton?> GetAsync(int id, string userId) => Task.FromResult<SharedAutomaton?>(null);
        public Task<List<SharedAutomaton>> ListForGroupAsync(int groupId, string userId) => Task.FromResult(new List<SharedAutomaton>());
        public Task<List<SharedAutomaton>> ListForUserAsync(string userId) => Task.FromResult(new List<SharedAutomaton>());
        public Task DeleteAsync(int id, string userId) => Task.CompletedTask;
        public Task<SharedAutomaton> UpdateAsync(int id, string userId, string? name, string? description, AutomatonViewModel? model) => throw new NotImplementedException();
        public Task<SharedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description) => throw new NotImplementedException();
        public Task<SharedAutomatonGroup?> GetGroupAsync(int groupId, string userId) => Task.FromResult<SharedAutomatonGroup?>(null);
        public Task DeleteGroupAsync(int groupId, string userId) => Task.CompletedTask;
        public Task UpdateGroupAsync(int groupId, string userId, string? name, string? description) => Task.CompletedTask;
        public Task<List<SharedAutomatonGroupMember>> ListGroupMembersAsync(int groupId, string userId) => Task.FromResult(new List<SharedAutomatonGroupMember>());
        public Task RemoveMemberAsync(int groupId, string userId, string memberUserId) => Task.CompletedTask;
        public Task UpdateMemberRoleAsync(int groupId, string userId, string memberUserId, SharedGroupRole newRole) => Task.CompletedTask;
        public Task<bool> CanUserViewGroupAsync(int groupId, string userId) => Task.FromResult(true);
        public Task<bool> CanUserAddToGroupAsync(int groupId, string userId) => Task.FromResult(true);
        public Task<bool> CanUserEditInGroupAsync(int groupId, string userId) => Task.FromResult(true);
        public Task<bool> CanUserManageMembersAsync(int groupId, string userId) => Task.FromResult(true);
        public Task<SharedGroupRole?> GetUserRoleInGroupAsync(int groupId, string userId) => Task.FromResult<SharedGroupRole?>(SharedGroupRole.Owner);
    }

    [Fact]
    public async Task Save_ForwardsLayoutAndThumbnailToSavedService()
    {
        var user = new ApplicationUser { Id = "u-save" };
        var savedSvc = new RecordingSavedAutomatonService();
        var sharedSvc = new SimpleSharedAutomatonService();
        var tempDataSvc = new MockAutomatonTempDataService();
        var fileSvc = new MockAutomatonFileService();
        var userManager = new TestUserManager(user);

        var controller = new SavedAutomatonController(savedSvc, sharedSvc, tempDataSvc, fileSvc, userManager);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new Microsoft.AspNetCore.Mvc.ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity(new[] { new Claim(ClaimTypes.NameIdentifier, user.Id) }));

        var model = new AutomatonViewModel { Type = AutomatonType.DFA };
        var layout = "[{\"id\":\"0\",\"position\":{\"x\":1,\"y\":2}}]";
        var thumb = "iVBORw0KGgoAAAANS...";

        var result = await controller.Save(model, "nm", null, saveState: false, layoutJson: layout, thumbnailBase64: thumb) as Microsoft.AspNetCore.Mvc.RedirectToActionResult;

        result.ShouldNotBeNull();
        savedSvc.LastLayoutJson.ShouldBe(layout);
        savedSvc.LastThumbnailBase64.ShouldBe(thumb);
    }
}
