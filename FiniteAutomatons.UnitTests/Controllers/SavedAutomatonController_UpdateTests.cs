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

/// <summary>
/// Unit tests for SavedAutomatonController.Update and the Load→TempData metadata feature.
/// </summary>
public class SavedAutomatonController_UpdateTests
{
    private const string TestUserId = "user-update-tests";

    #region Infrastructure

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
        public Task<ApplicationUser?> FindByIdAsync(string id, CancellationToken ct) => Task.FromResult<ApplicationUser?>(null);
        public Task<ApplicationUser?> FindByNameAsync(string n, CancellationToken ct) => Task.FromResult<ApplicationUser?>(null);
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult<string?>(u.UserName?.ToUpperInvariant());
        public Task<string> GetUserIdAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(u.Id);
        public Task<string?> GetUserNameAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult<string?>(u.UserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser u, string? v, CancellationToken ct) { u.NormalizedUserName = v; return Task.CompletedTask; }
        public Task SetUserNameAsync(ApplicationUser u, string? v, CancellationToken ct) { u.UserName = v; return Task.CompletedTask; }
        public Task<IdentityResult> UpdateAsync(ApplicationUser u, CancellationToken ct) => Task.FromResult(IdentityResult.Success);
    }

    private sealed class TestUserManager(ApplicationUser? user) : UserManager<ApplicationUser>(
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
        private readonly ApplicationUser? _user = user;
        public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal) => Task.FromResult(_user);
    }

    /// <summary>Recording service that captures arguments passed to UpdateAsync.</summary>
    private class RecordingUpdateService : ISavedAutomatonService
    {
        public int LastUpdatedId { get; private set; }
        public string? LastUpdatedName { get; private set; }
        public string? LastUpdatedDescription { get; private set; }
        public bool LastSaveExecutionState { get; private set; }
        public AutomatonViewModel? LastModel { get; private set; }
        public string? LastLayoutJson { get; private set; }
        public string? LastThumbnailBase64 { get; private set; }
        public bool UpdateCalled { get; private set; }

        public Task<SavedAutomaton> UpdateAsync(int id, string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, string? layoutJson = null, string? thumbnailBase64 = null)
        {
            UpdateCalled = true;
            LastUpdatedId = id;
            LastUpdatedName = name;
            LastUpdatedDescription = description;
            LastSaveExecutionState = saveExecutionState;
            LastModel = new AutomatonViewModel
            {
                Input = model.Input,
                HasExecuted = model.HasExecuted,
                Position = model.Position,
                CurrentStateId = model.CurrentStateId,
                IsAccepted = model.IsAccepted,
                StateHistorySerialized = model.StateHistorySerialized,
                StackSerialized = model.StackSerialized,
            };
            LastLayoutJson = layoutJson;
            LastThumbnailBase64 = thumbnailBase64;
            return Task.FromResult(new SavedAutomaton { Id = id, UserId = userId, Name = name, ContentJson = "{}" });
        }

        // NotFound scenario: throw InvalidOperationException
        public bool ThrowOnUpdate { get; set; }

        // Other members not needed for Update tests
        public Task<SavedAutomaton> SaveAsync(string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, int? groupId = null, string? layoutJson = null, string? thumbnailBase64 = null)
            => Task.FromResult(new SavedAutomaton { Id = 99, UserId = userId, Name = name, ContentJson = "{}" });
        public Task<List<SavedAutomaton>> ListForUserAsync(string userId, int? groupId = null) => Task.FromResult(new List<SavedAutomaton>());
        public Task<SavedAutomaton?> GetAsync(int id, string userId) => Task.FromResult<SavedAutomaton?>(null);
        public Task DeleteAsync(int id, string userId) => Task.CompletedTask;
        public Task<SavedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description) => throw new NotImplementedException();
        public Task<List<SavedAutomatonGroup>> ListGroupsForUserAsync(string userId) => Task.FromResult(new List<SavedAutomatonGroup>());
        public Task AddGroupMemberAsync(int groupId, string userId) => Task.CompletedTask;
        public Task RemoveGroupMemberAsync(int groupId, string userId) => Task.CompletedTask;
        public Task<List<SavedAutomatonGroupMember>> ListGroupMembersAsync(int groupId) => Task.FromResult(new List<SavedAutomatonGroupMember>());
        public Task<bool> CanUserSaveToGroupAsync(int groupId, string userId) => Task.FromResult(true);
        public Task<SavedAutomatonGroup?> GetGroupAsync(int groupId) => Task.FromResult<SavedAutomatonGroup?>(null);
        public Task SetGroupSharingPolicyAsync(int groupId, bool membersCanShare) => Task.CompletedTask;
        public Task DeleteGroupAsync(int id, string userId) => Task.CompletedTask;
        public Task AssignAutomatonToGroupAsync(int automatonId, string userId, int? groupId) => Task.CompletedTask;
        public Task RemoveAutomatonFromGroupAsync(int automatonId, string userId, int groupId) => Task.CompletedTask;
    }

    private sealed class ThrowingUpdateService : ISavedAutomatonService
    {
        public Task<SavedAutomaton> UpdateAsync(int id, string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, string? layoutJson = null, string? thumbnailBase64 = null)
            => throw new InvalidOperationException("Automaton not found.");
        public Task<SavedAutomaton> SaveAsync(string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, int? groupId = null, string? layoutJson = null, string? thumbnailBase64 = null)
            => throw new NotImplementedException();
        public Task<List<SavedAutomaton>> ListForUserAsync(string userId, int? groupId = null) => Task.FromResult(new List<SavedAutomaton>());
        public Task<SavedAutomaton?> GetAsync(int id, string userId) => Task.FromResult<SavedAutomaton?>(null);
        public Task DeleteAsync(int id, string userId) => Task.CompletedTask;
        public Task<SavedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description) => throw new NotImplementedException();
        public Task<List<SavedAutomatonGroup>> ListGroupsForUserAsync(string userId) => Task.FromResult(new List<SavedAutomatonGroup>());
        public Task AddGroupMemberAsync(int groupId, string userId) => Task.CompletedTask;
        public Task RemoveGroupMemberAsync(int groupId, string userId) => Task.CompletedTask;
        public Task<List<SavedAutomatonGroupMember>> ListGroupMembersAsync(int groupId) => Task.FromResult(new List<SavedAutomatonGroupMember>());
        public Task<bool> CanUserSaveToGroupAsync(int groupId, string userId) => Task.FromResult(true);
        public Task<SavedAutomatonGroup?> GetGroupAsync(int groupId) => Task.FromResult<SavedAutomatonGroup?>(null);
        public Task SetGroupSharingPolicyAsync(int groupId, bool membersCanShare) => Task.CompletedTask;
        public Task DeleteGroupAsync(int id, string userId) => Task.CompletedTask;
        public Task AssignAutomatonToGroupAsync(int automatonId, string userId, int? groupId) => Task.CompletedTask;
        public Task RemoveAutomatonFromGroupAsync(int automatonId, string userId, int groupId) => Task.CompletedTask;
    }

    private sealed class StubSharedService : ISharedAutomatonService
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

    private static (SavedAutomatonController controller, RecordingUpdateService savedSvc, TempDataDictionary tempData)
        BuildController(RecordingUpdateService? savedSvc = null)
    {
        var user = new ApplicationUser { Id = TestUserId };
        savedSvc ??= new RecordingUpdateService();

        var controller = new SavedAutomatonController(
            savedSvc,
            new StubSharedService(),
            new MockAutomatonTempDataService(),
            new MockAutomatonFileService(),
            new TestUserManager(user));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, TestUserId)]))
        };
        var td = new TempDataDictionary(httpContext, new TestTempDataProvider());
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = td;

        return (controller, savedSvc, td);
    }

    private static SavedAutomaton BuildSavedAutomaton(int id, string name = "My Automaton", string? description = "My Desc")
    {
        var payload = new AutomatonPayloadDto
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };
        return new SavedAutomaton
        {
            Id = id,
            UserId = TestUserId,
            Name = name,
            Description = description,
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.Structure,
        };
    }

    #endregion

    // ── Update action ────────────────────────────────────────────────────────

    [Fact]
    public async Task Update_ValidRequest_CallsUpdateAsyncWithCorrectId()
    {
        var (controller, savedSvc, _) = BuildController();
        var model = new AutomatonViewModel { Type = AutomatonType.DFA };

        await controller.Update(model, id: 42, name: "Updated Name", description: "Updated Desc", saveState: false);

        savedSvc.UpdateCalled.ShouldBeTrue();
        savedSvc.LastUpdatedId.ShouldBe(42);
        savedSvc.LastUpdatedName.ShouldBe("Updated Name");
        savedSvc.LastUpdatedDescription.ShouldBe("Updated Desc");
    }

    [Fact]
    public async Task Update_ValidRequest_RedirectsToIndex()
    {
        var (controller, _, _) = BuildController();
        var model = new AutomatonViewModel { Type = AutomatonType.DFA };

        var result = await controller.Update(model, id: 1, name: "Name", description: null);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe("Index");
    }

    [Fact]
    public async Task Update_ValidRequest_SetsTempDataSuccessMessage()
    {
        var (controller, _, td) = BuildController();
        var model = new AutomatonViewModel { Type = AutomatonType.DFA };

        await controller.Update(model, id: 1, name: "Name", description: null);

        td["ConversionMessage"].ShouldBe("Automaton updated successfully.");
    }

    [Fact]
    public async Task Update_ForwardsLayoutAndThumbnailToService()
    {
        var (controller, savedSvc, _) = BuildController();
        var model = new AutomatonViewModel { Type = AutomatonType.DFA };
        var layout = "[{\"id\":\"1\",\"position\":{\"x\":10,\"y\":20}}]";
        var thumb = "data:image/png;base64,abc123";

        await controller.Update(model, id: 5, name: "N", description: null, layoutJson: layout, thumbnailBase64: thumb);

        savedSvc.LastLayoutJson.ShouldBe(layout);
        savedSvc.LastThumbnailBase64.ShouldBe(thumb);
    }

    [Fact]
    public async Task Update_SaveMode_Structure_ClearsInputAndExecutionState()
    {
        var (controller, savedSvc, _) = BuildController();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            Input = "abc",
            HasExecuted = true,
            Position = 2,
            CurrentStateId = 3,
            IsAccepted = true,
            StateHistorySerialized = "[{\"StateId\":1}]",
            StackSerialized = "stack"
        };

        await controller.Update(model, id: 1, name: "N", description: null, saveMode: "structure");

        var m = savedSvc.LastModel.ShouldNotBeNull();
        m.Input.ShouldBeNullOrEmpty();
        m.HasExecuted.ShouldBeFalse();
        m.Position.ShouldBe(0);
        m.CurrentStateId.ShouldBeNull();
        m.IsAccepted.ShouldBeNull();
        m.StateHistorySerialized.ShouldBe(string.Empty);
        m.StackSerialized.ShouldBeNull();
        savedSvc.LastSaveExecutionState.ShouldBeFalse();
    }

    [Fact]
    public async Task Update_SaveMode_Input_KeepsInputButClearsExecutionState()
    {
        var (controller, savedSvc, _) = BuildController();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            Input = "hello",
            HasExecuted = true,
            Position = 3,
            CurrentStateId = 2,
            IsAccepted = false,
            StateHistorySerialized = "hist",
            StackSerialized = "st"
        };

        await controller.Update(model, id: 1, name: "N", description: null, saveMode: "input");

        var m = savedSvc.LastModel.ShouldNotBeNull();
        m.Input.ShouldBe("hello");
        m.HasExecuted.ShouldBeFalse();
        m.Position.ShouldBe(0);
        m.CurrentStateId.ShouldBeNull();
        m.IsAccepted.ShouldBeNull();
        savedSvc.LastSaveExecutionState.ShouldBeFalse();
    }

    [Fact]
    public async Task Update_SaveMode_State_PreservesFullExecutionState()
    {
        var (controller, savedSvc, _) = BuildController();
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            Input = "ab",
            HasExecuted = true,
            Position = 2,
            CurrentStateId = 4,
            IsAccepted = true,
            StateHistorySerialized = "[{\"StateId\":1},{\"StateId\":3}]",
            StackSerialized = null
        };

        await controller.Update(model, id: 1, name: "N", description: null, saveMode: "state");

        savedSvc.LastSaveExecutionState.ShouldBeTrue();
        var m = savedSvc.LastModel.ShouldNotBeNull();
        m.Input.ShouldBe("ab");
        m.HasExecuted.ShouldBeTrue();
        m.Position.ShouldBe(2);
        m.CurrentStateId.ShouldBe(4);
        m.IsAccepted.ShouldBe(true);
    }

    [Fact]
    public async Task Update_MissingName_ReturnsViewWithModelError()
    {
        var (controller, savedSvc, _) = BuildController();
        var model = new AutomatonViewModel { Type = AutomatonType.DFA };

        var result = await controller.Update(model, id: 1, name: "   ", description: null);

        result.ShouldBeOfType<ViewResult>();
        savedSvc.UpdateCalled.ShouldBeFalse();
    }

    [Fact]
    public async Task Update_ServiceThrowsInvalidOperation_SetsErrorTempDataAndRedirects()
    {
        var user = new ApplicationUser { Id = TestUserId };
        ISavedAutomatonService throwingSvc = new ThrowingUpdateService();
        var controller = new SavedAutomatonController(
            throwingSvc,
            new StubSharedService(),
            new MockAutomatonTempDataService(),
            new MockAutomatonFileService(),
            new TestUserManager(user));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, TestUserId)]))
        };
        var td = new TempDataDictionary(httpContext, new TestTempDataProvider());
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = td;

        var result = await controller.Update(new AutomatonViewModel(), id: 99, name: "X", description: null);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe("Index");
        (td["ConversionMessage"] as string).ShouldNotBeNull();
        (td["ConversionMessage"] as string)!.ShouldContain("could not be updated");
    }

    [Fact]
    public async Task Update_TrimsNameAndDescription()
    {
        var (controller, savedSvc, _) = BuildController();
        var model = new AutomatonViewModel { Type = AutomatonType.DFA };

        await controller.Update(model, id: 1, name: "  Padded Name  ", description: "  Padded Desc  ", saveState: false);

        savedSvc.LastUpdatedName.ShouldBe("Padded Name");
        savedSvc.LastUpdatedDescription.ShouldBe("Padded Desc");
    }

    [Fact]
    public async Task Update_EmptyDescription_PassesNullToService()
    {
        var (controller, savedSvc, _) = BuildController();
        var model = new AutomatonViewModel { Type = AutomatonType.DFA };

        await controller.Update(model, id: 1, name: "N", description: "   ", saveState: false);

        savedSvc.LastUpdatedDescription.ShouldBeNull();
    }

    [Fact]
    public async Task Update_WhenUserMissing_ReturnsChallenge()
    {
        var controller = new SavedAutomatonController(
            new RecordingUpdateService(),
            new StubSharedService(),
            new MockAutomatonTempDataService(),
            new MockAutomatonFileService(),
            new TestUserManager(null));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity())
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());

        var result = await controller.Update(new AutomatonViewModel(), id: 1, name: "Name", description: null);

        result.ShouldBeOfType<ChallengeResult>();
    }

    // ── Load → TempData metadata ─────────────────────────────────────────────

    [Fact]
    public async Task Load_SetsLoadedAutomatonIdOnModel()
    {
        var entity = BuildSavedAutomaton(id: 7);
        var stubSvc = new StubSavedAutomatonServiceForLoad(entity);
        var user = new ApplicationUser { Id = TestUserId };
        var capturingSvc = new CapturingTempDataService();

        var controller = new SavedAutomatonController(
            stubSvc,
            new StubSharedService(),
            capturingSvc,
            new MockAutomatonFileService(),
            new TestUserManager(user));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, TestUserId)]))
        };
        var td = new TempDataDictionary(httpContext, new TestTempDataProvider());
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = td;

        await controller.Load(7);

        capturingSvc.LastStoredModel.ShouldNotBeNull();
        capturingSvc.LastStoredModel!.LoadedAutomatonId.ShouldBe(7);
    }

    [Fact]
    public async Task Load_StoresOriginalNameInTempData()
    {
        var entity = BuildSavedAutomaton(id: 3, name: "DFA Even As");
        var stubSvc = new StubSavedAutomatonServiceForLoad(entity);
        var user = new ApplicationUser { Id = TestUserId };
        var capturingSvc = new CapturingTempDataService();

        var controller = new SavedAutomatonController(
            stubSvc,
            new StubSharedService(),
            capturingSvc,
            new MockAutomatonFileService(),
            new TestUserManager(user));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, TestUserId)]))
        };
        var td = new TempDataDictionary(httpContext, new TestTempDataProvider());
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = td;

        await controller.Load(3);

        td["LoadedAutomatonName"].ShouldBe("DFA Even As");
    }

    [Fact]
    public async Task Load_StoresOriginalDescriptionInTempData()
    {
        var entity = BuildSavedAutomaton(id: 5, name: "Test", description: "Accepts even number of a's");
        var stubSvc = new StubSavedAutomatonServiceForLoad(entity);
        var user = new ApplicationUser { Id = TestUserId };
        var capturingSvc = new CapturingTempDataService();

        var controller = new SavedAutomatonController(
            stubSvc,
            new StubSharedService(),
            capturingSvc,
            new MockAutomatonFileService(),
            new TestUserManager(user));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, TestUserId)]))
        };
        var td = new TempDataDictionary(httpContext, new TestTempDataProvider());
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = td;

        await controller.Load(5);

        td["LoadedAutomatonDescription"].ShouldBe("Accepts even number of a's");
    }

    [Fact]
    public async Task Load_NullDescription_StoresNullInTempData()
    {
        var entity = BuildSavedAutomaton(id: 2, name: "No Desc", description: null);
        var stubSvc = new StubSavedAutomatonServiceForLoad(entity);
        var user = new ApplicationUser { Id = TestUserId };
        var capturingSvc = new CapturingTempDataService();

        var controller = new SavedAutomatonController(
            stubSvc,
            new StubSharedService(),
            capturingSvc,
            new MockAutomatonFileService(),
            new TestUserManager(user));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, TestUserId)]))
        };
        var td = new TempDataDictionary(httpContext, new TestTempDataProvider());
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = td;

        await controller.Load(2);

        td["LoadedAutomatonDescription"].ShouldBeNull();
    }

    [Fact]
    public async Task Load_WithLayoutJson_StoresLayoutJsonInTempData()
    {
        var entity = BuildSavedAutomaton(id: 8);
        entity.LayoutJson = "[{\"id\":\"1\",\"position\":{\"x\":10,\"y\":20}}]";
        var stubSvc = new StubSavedAutomatonServiceForLoad(entity);
        var user = new ApplicationUser { Id = TestUserId };
        var capturingSvc = new CapturingTempDataService();

        var controller = new SavedAutomatonController(
            stubSvc,
            new StubSharedService(),
            capturingSvc,
            new MockAutomatonFileService(),
            new TestUserManager(user));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, TestUserId)]))
        };
        var td = new TempDataDictionary(httpContext, new TestTempDataProvider());
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = td;

        await controller.Load(8);

        td["LayoutJson"].ShouldBe(entity.LayoutJson);
    }

    [Fact]
    public async Task Load_PreservesSourceRegexOnStoredModel()
    {
        var entity = BuildSavedAutomaton(id: 9);
        entity.SourceRegex = "a*b";
        var stubSvc = new StubSavedAutomatonServiceForLoad(entity);
        var user = new ApplicationUser { Id = TestUserId };
        var capturingSvc = new CapturingTempDataService();

        var controller = new SavedAutomatonController(
            stubSvc,
            new StubSharedService(),
            capturingSvc,
            new MockAutomatonFileService(),
            new TestUserManager(user));

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, TestUserId)]))
        };
        var td = new TempDataDictionary(httpContext, new TestTempDataProvider());
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = td;

        await controller.Load(9);

        capturingSvc.LastStoredModel.ShouldNotBeNull();
        capturingSvc.LastStoredModel!.SourceRegex.ShouldBe("a*b");
    }

    // ── Helpers shared by Load tests ─────────────────────────────────────────

    private sealed class CapturingTempDataService : IAutomatonTempDataService
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

        public (bool Success, AutomatonViewModel? Model) TryGetSessionAutomaton(ISession session, string sessionKey) => (false, null);
    }

    private sealed class StubSavedAutomatonServiceForLoad(SavedAutomaton? entity) : ISavedAutomatonService
    {
        private readonly SavedAutomaton? _entity = entity;

        public Task<SavedAutomaton?> GetAsync(int id, string userId) => Task.FromResult(_entity);
        public Task<SavedAutomaton> SaveAsync(string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, int? groupId = null, string? layoutJson = null, string? thumbnailBase64 = null)
            => Task.FromResult(new SavedAutomaton { Id = 1, UserId = userId, Name = name, ContentJson = "{}" });
        public Task<SavedAutomaton> UpdateAsync(int id, string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, string? layoutJson = null, string? thumbnailBase64 = null)
            => Task.FromResult(new SavedAutomaton { Id = id, UserId = userId, Name = name, ContentJson = "{}" });
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
        public Task DeleteGroupAsync(int id, string userId) => Task.CompletedTask;
        public Task AssignAutomatonToGroupAsync(int automatonId, string userId, int? groupId) => Task.CompletedTask;
        public Task RemoveAutomatonFromGroupAsync(int automatonId, string userId, int groupId) => Task.CompletedTask;
    }
}
