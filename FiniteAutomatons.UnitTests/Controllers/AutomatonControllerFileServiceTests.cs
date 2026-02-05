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

namespace FiniteAutomatons.UnitTests.Controllers;

public class AutomatonControllerFileServiceTests
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

    private class TestUserManager(IdentityUser? user) : UserManager<IdentityUser>(new FakeUserStore(), Microsoft.Extensions.Options.Options.Create(new IdentityOptions()), new PasswordHasher<IdentityUser>(), [], [], new UpperInvariantLookupNormalizer(), new IdentityErrorDescriber(), services: null!, logger: new NullLogger<UserManager<IdentityUser>>())
    {
        private readonly IdentityUser? userToReturn = user;

        public override Task<IdentityUser?> GetUserAsync(ClaimsPrincipal principal)
        {
            return Task.FromResult(userToReturn);
        }
    }

    private class MockSavedAutomatonService : ISavedAutomatonService
    {
        public Task<SavedAutomaton> SaveAsync(string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, int? groupId = null) => throw new NotImplementedException();
        public Task<List<SavedAutomaton>> ListForUserAsync(string userId, int? groupId = null) => throw new NotImplementedException();
        public Task<SavedAutomaton?> GetAsync(int id, string userId) => throw new NotImplementedException();
        public Task DeleteAsync(int id, string userId) => throw new NotImplementedException();
        public Task<SavedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description) => throw new NotImplementedException();
        public Task<List<SavedAutomatonGroup>> ListGroupsForUserAsync(string userId) => throw new NotImplementedException();
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

    [Fact]
    public async Task ImportAutomaton_NoFile_ShowsError()
    {
        var controller = Build();
        var result = await controller.ImportAutomaton(null!);
        result.ShouldBeOfType<BadRequestObjectResult>();
    }

    private static ImportExportController Build()
    {
        var fileSvc = new MockAutomatonFileService();
        var savedAutomatonSvc = new MockSavedAutomatonService();
        var user = new IdentityUser { Id = "test-user", UserName = "test@example.com" };
        var userManager = new TestUserManager(user);

        var controller = new ImportExportController(fileSvc, savedAutomatonSvc, userManager)
        {
            TempData = new TempDataDictionary(new DefaultHttpContext(), new TestTempDataProvider())
        };

        return controller;
    }
}
