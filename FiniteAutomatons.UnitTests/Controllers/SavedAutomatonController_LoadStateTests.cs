using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.DTOs;
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

public class SavedAutomatonController_LoadStateTests
{
    private const string TestUserId = "user-load-state";

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
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
        public void Dispose() { }
        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken ct) => Task.FromResult<ApplicationUser?>(null);
        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken ct) => Task.FromResult<ApplicationUser?>(null);
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult<string?>(user.UserName?.ToUpperInvariant());
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(user.Id);
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult<string?>(user.UserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? n, CancellationToken ct) { user.NormalizedUserName = n; return Task.CompletedTask; }
        public Task SetUserNameAsync(ApplicationUser user, string? name, CancellationToken ct) { user.UserName = name; return Task.CompletedTask; }
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
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
        private readonly ApplicationUser? user = user;
        public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal) => Task.FromResult(user);
    }

    private class CapturingTempDataService : IAutomatonTempDataService
    {
        public AutomatonViewModel? LastStoredModel { get; private set; }

        public (bool Success, AutomatonViewModel? Model) TryGetCustomAutomaton(ITempDataDictionary tempData) => (false, null);

        public void StoreCustomAutomaton(ITempDataDictionary tempData, AutomatonViewModel model)
        {
            LastStoredModel = model;
            tempData["CustomAutomaton"] = JsonSerializer.Serialize(model);
        }

        public void StoreErrorMessage(ITempDataDictionary tempData, string errorMessage) =>
            tempData["ErrorMessage"] = errorMessage;

        public void StoreConversionMessage(ITempDataDictionary tempData, string message) =>
            tempData["ConversionMessage"] = message;
    }

    private class StubSavedAutomatonService(SavedAutomaton? entity) : ISavedAutomatonService
    {
        private readonly SavedAutomaton? _entity = entity;
        public Task<SavedAutomaton?> GetAsync(int id, string userId) => Task.FromResult(_entity);
        public Task<SavedAutomaton> SaveAsync(string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, int? groupId = null, string? layoutJson = null, string? thumbnailBase64 = null)
            => Task.FromResult(new SavedAutomaton { Id = 1, UserId = userId, Name = name, ContentJson = "{}" });
        public Task DeleteAsync(int id, string userId) => Task.CompletedTask;
        public Task<List<SavedAutomaton>> ListForUserAsync(string userId, int? groupId = null) => Task.FromResult(new List<SavedAutomaton>());
        public Task<SavedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description) => throw new NotImplementedException();
        public Task<List<SavedAutomatonGroup>> ListGroupsForUserAsync(string userId) => Task.FromResult(new List<SavedAutomatonGroup>());
        public Task AddGroupMemberAsync(int groupId, string userId) => Task.CompletedTask;
        public Task RemoveGroupMemberAsync(int groupId, string userId) => Task.CompletedTask;
        public Task<List<SavedAutomatonGroupMember>> ListGroupMembersAsync(int groupId) => Task.FromResult(new List<SavedAutomatonGroupMember>());
        public Task<bool> CanUserSaveToGroupAsync(int groupId, string userId) => Task.FromResult(true);
        public Task<SavedAutomatonGroup?> GetGroupAsync(int groupId) => Task.FromResult<SavedAutomatonGroup?>(null);
        public Task SetGroupSharingPolicyAsync(int groupId, bool membersCanShare) => Task.CompletedTask;
        public static Task<List<SavedAutomaton>> ListByGroupAsync() => Task.FromResult(new List<SavedAutomaton>());
        public static Task<SavedAutomatonGroup?> GetGroupAsync() => Task.FromResult<SavedAutomatonGroup?>(null);
        public Task DeleteGroupAsync(int id, string userId) => Task.CompletedTask;
        public static Task<SavedAutomatonGroupMember?> GetGroupMemberAsync() => Task.FromResult<SavedAutomatonGroupMember?>(null);
        public Task AssignAutomatonToGroupAsync(int automatonId, string userId, int? groupId) => Task.CompletedTask;
        public Task RemoveAutomatonFromGroupAsync(int automatonId, string userId, int groupId) => Task.CompletedTask;
    }

    private class StubSharedAutomatonService : ISharedAutomatonService
    {
        public Task<SharedAutomaton> SaveAsync(string userId, int groupId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, string? layoutJson = null, string? thumbnailBase64 = null)
            => Task.FromResult(new SharedAutomaton { CreatedByUserId = userId, Name = name, ContentJson = "{}" });
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

    private static SavedAutomaton BuildEntityWithState(AutomatonSaveMode saveMode, SavedExecutionStateDto? execState)
    {
        var payload = new AutomatonPayloadDto
        {
            Type = AutomatonType.DFA,
            States = [new Core.Models.DoMain.State { Id = 1, IsStart = true, IsAccepting = false }, new Core.Models.DoMain.State { Id = 2, IsStart = false, IsAccepting = true }],
            Transitions = [new Core.Models.DoMain.Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' }]
        };

        return new SavedAutomaton
        {
            Id = 1,
            UserId = TestUserId,
            Name = "Test",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = saveMode,
            ExecutionStateJson = execState != null ? JsonSerializer.Serialize(execState) : null
        };
    }

    private static (SavedAutomatonController controller, CapturingTempDataService tempSvc) BuildController(SavedAutomaton? entity)
    {
        var user = new ApplicationUser { Id = TestUserId };
        var userManager = new TestUserManager(user);
        var tempSvc = new CapturingTempDataService();
        var savedSvc = new StubSavedAutomatonService(entity);
        var sharedSvc = new StubSharedAutomatonService();
        var fileSvc = new MockAutomatonFileService();

        var controller = new SavedAutomatonController(savedSvc, sharedSvc, tempSvc, fileSvc, userManager);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, TestUserId)]))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());

        return (controller, tempSvc);
    }

    #endregion

    [Fact]
    public async Task Load_WithModeState_AndWithStateEntity_SetsHasExecutedTrue()
    {
        // Arrange
        var execState = new SavedExecutionStateDto
        {
            Input = "ab",
            Position = 1,
            CurrentStateId = 2,
            IsAccepted = null,
            StateHistorySerialized = "[{\"StateId\":1}]"
        };
        var entity = BuildEntityWithState(AutomatonSaveMode.WithState, execState);
        var (controller, tempSvc) = BuildController(entity);

        // Act
        var result = await controller.Load(entity.Id, mode: "state");

        // Assert: redirects to Home/Index
        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe("Index");
        redirect.ControllerName.ShouldBe("Home");

        // Assert: execution state fully restored including HasExecuted
        var model = tempSvc.LastStoredModel;
        model.ShouldNotBeNull();
        model.HasExecuted.ShouldBeTrue();
        model.Input.ShouldBe("ab");
        model.Position.ShouldBe(1);
        model.CurrentStateId.ShouldBe(2);
        model.StateHistorySerialized.ShouldBe("[{\"StateId\":1}]");
    }

    [Fact]
    public async Task Load_WithModeState_AndWithStateEntity_RestoresHistoryCorrectly()
    {
        // Arrange: execution in the middle of input "1222" at position 3 with history
        var historyJson = "[{\"StateId\":1},{\"StateId\":2},{\"StateId\":3}]";
        var execState = new SavedExecutionStateDto
        {
            Input = "1222",
            Position = 3,
            CurrentStateId = 3,
            IsAccepted = false,
            StateHistorySerialized = historyJson
        };
        var entity = BuildEntityWithState(AutomatonSaveMode.WithState, execState);
        var (controller, tempSvc) = BuildController(entity);

        // Act
        await controller.Load(entity.Id, mode: "state");

        // Assert: history is preserved (enables Back navigation)
        var model = tempSvc.LastStoredModel;
        model.ShouldNotBeNull();
        model.HasExecuted.ShouldBeTrue();
        model.Position.ShouldBe(3);
        model.StateHistorySerialized.ShouldBe(historyJson);
        model.IsAccepted.ShouldBe(false);
    }

    [Fact]
    public async Task Load_WithModeInput_DoesNotSetHasExecuted()
    {
        // Arrange: mode=input should only restore input string, not full state
        var execState = new SavedExecutionStateDto
        {
            Input = "hello",
            Position = 3,
            CurrentStateId = 2,
            StateHistorySerialized = "[{\"StateId\":1},{\"StateId\":2}]"
        };
        var entity = BuildEntityWithState(AutomatonSaveMode.WithState, execState);
        var (controller, tempSvc) = BuildController(entity);

        // Act
        await controller.Load(entity.Id, mode: "input");

        // Assert: input loaded, but execution state is NOT restored
        var model = tempSvc.LastStoredModel;
        model.ShouldNotBeNull();
        model.Input.ShouldBe("hello");
        model.HasExecuted.ShouldBeFalse();
        model.Position.ShouldBe(0);
        model.CurrentStateId.ShouldBeNull();
    }

    [Fact]
    public async Task Load_WithModeStructure_LoadsOnlyStructure()
    {
        // Arrange: mode=structure should ignore execution state entirely
        var execState = new SavedExecutionStateDto { Input = "test", Position = 2, CurrentStateId = 1 };
        var entity = BuildEntityWithState(AutomatonSaveMode.WithState, execState);
        var (controller, tempSvc) = BuildController(entity);

        // Act
        await controller.Load(entity.Id, mode: "structure");

        // Assert: no input or execution state loaded
        var model = tempSvc.LastStoredModel;
        model.ShouldNotBeNull();
        model.HasExecuted.ShouldBeFalse();
        model.Input.ShouldBeNullOrEmpty();
        model.Position.ShouldBe(0);
    }

    [Fact]
    public async Task Load_WithModeState_ButEntityHasNoExecutionState_DoesNotSetHasExecuted()
    {
        // Arrange: entity was saved as WithState but ExecutionStateJson is empty
        var entity = BuildEntityWithState(AutomatonSaveMode.WithState, execState: null);
        var (controller, tempSvc) = BuildController(entity);

        // Act
        await controller.Load(entity.Id, mode: "state");

        // Assert: HasExecuted stays false since there's nothing to restore
        var model = tempSvc.LastStoredModel;
        model.ShouldNotBeNull();
        model.HasExecuted.ShouldBeFalse();
    }

    [Fact]
    public async Task Load_WithModeState_ButEntitySavedAsStructureMode_DoesNotSetHasExecuted()
    {
        // Arrange: entity was saved as Structure-only — mode=state should be ignored
        var execState = new SavedExecutionStateDto { Input = "hi", Position = 1, CurrentStateId = 2 };
        var entity = BuildEntityWithState(AutomatonSaveMode.Structure, execState);
        var (controller, tempSvc) = BuildController(entity);

        // Act
        await controller.Load(entity.Id, mode: "state");

        // Assert: even though mode asks for state, SaveMode=Structure means state is not loaded
        var model = tempSvc.LastStoredModel;
        model.ShouldNotBeNull();
        model.HasExecuted.ShouldBeFalse();
        model.Position.ShouldBe(0);
    }
}
