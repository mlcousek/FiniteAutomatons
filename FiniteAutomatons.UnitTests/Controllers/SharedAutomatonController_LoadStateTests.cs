using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.DTOs;
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
using System.Text.Json;

namespace FiniteAutomatons.UnitTests.Controllers;

public class SharedAutomatonController_LoadStateTests : IDisposable
{
    private const string TestUserId = "user-shared-load-state";

    private readonly ApplicationDbContext context;
    private readonly SharedAutomatonController controller;
    private readonly CapturingTempDataService tempSvc;
    private readonly ISharedAutomatonService sharedService;

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
        private readonly ApplicationUser? _user = user;
        public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal) => Task.FromResult(_user);
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

    private class MockInvitationNotificationService : IInvitationNotificationService
    {
        public Task<List<SharedAutomatonGroupInvitation>> GetPendingInvitationsForUserAsync(string userId, string email) =>
            Task.FromResult(new List<SharedAutomatonGroupInvitation>());
        public Task<bool> HasInvitationNotificationsEnabledAsync(string userId) => Task.FromResult(false);
        public Task SetInvitationNotificationsAsync(string userId, bool enabled) => Task.CompletedTask;
    }

    #endregion

    public SharedAutomatonController_LoadStateTests()
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(Guid.NewGuid().ToString())
            .Options;

        context = new ApplicationDbContext(options);

        var testUser = new ApplicationUser { Id = TestUserId, UserName = TestUserId, Email = TestUserId };
        var userManager = new TestUserManager(testUser);

        sharedService = new SharedAutomatonService(context, NullLogger<SharedAutomatonService>.Instance);
        var sharingService = new SharedAutomatonSharingService(
            context,
            sharedService,
            userManager,
            NullLogger<SharedAutomatonSharingService>.Instance);

        tempSvc = new CapturingTempDataService();

        controller = new SharedAutomatonController(
            sharedService,
            sharingService,
            tempSvc,
            userManager,
            context,
            new MockInvitationNotificationService(),
            NullLogger<SharedAutomatonController>.Instance);

        var httpContext = new DefaultHttpContext
        {
            User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, TestUserId)]))
        };
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
    }

    public void Dispose()
    {
        context.Database.EnsureDeleted();
        context.Dispose();
        GC.SuppressFinalize(this);
    }

    #region Helper Methods

    private async Task<(SharedAutomatonGroup group, SharedAutomaton automaton)> CreateGroupAndAutomaton(
        SavedExecutionStateDto? execState,
        AutomatonSaveMode saveMode)
    {
        var group = new SharedAutomatonGroup
        {
            UserId = TestUserId,
            Name = $"Group_{Guid.NewGuid():N}",
            CreatedAt = DateTime.UtcNow
        };
        context.SharedAutomatonGroups.Add(group);
        await context.SaveChangesAsync();

        context.SharedAutomatonGroupMembers.Add(new SharedAutomatonGroupMember
        {
            GroupId = group.Id,
            UserId = TestUserId,
            Role = SharedGroupRole.Owner,
            JoinedAt = DateTime.UtcNow
        });

        var payload = new AutomatonPayloadDto
        {
            Type = AutomatonType.DFA,
            States =
            [
                new Core.Models.DoMain.State { Id = 1, IsStart = true, IsAccepting = false },
                new Core.Models.DoMain.State { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions = [new Core.Models.DoMain.Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a' }]
        };

        var automaton = new SharedAutomaton
        {
            CreatedByUserId = TestUserId,
            Name = "Test",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = saveMode,
            ExecutionStateJson = execState != null ? JsonSerializer.Serialize(execState) : null,
            CreatedAt = DateTime.UtcNow
        };
        context.SharedAutomatons.Add(automaton);
        await context.SaveChangesAsync();

        context.SharedAutomatonGroupAssignments.Add(new SharedAutomatonGroupAssignment
        {
            AutomatonId = automaton.Id,
            GroupId = group.Id
        });
        await context.SaveChangesAsync();

        return (group, automaton);
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
        var (_, automaton) = await CreateGroupAndAutomaton(execState, AutomatonSaveMode.WithState);

        // Act
        var result = await controller.Load(automaton.Id, mode: "state");

        // Assert: redirects home
        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe("Index");
        redirect.ControllerName.ShouldBe("Home");

        // Assert: execution state fully restored including HasExecuted flag
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
        // Arrange
        var historyJson = "[{\"StateId\":1},{\"StateId\":2},{\"StateId\":3}]";
        var execState = new SavedExecutionStateDto
        {
            Input = "1222",
            Position = 3,
            CurrentStateId = 3,
            IsAccepted = false,
            StateHistorySerialized = historyJson
        };
        var (_, automaton) = await CreateGroupAndAutomaton(execState, AutomatonSaveMode.WithState);

        // Act
        await controller.Load(automaton.Id, mode: "state");

        // Assert: history preserved (enables Back navigation)
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
        // Arrange: mode=input only loads the input string, not the full state
        var execState = new SavedExecutionStateDto
        {
            Input = "hello",
            Position = 3,
            CurrentStateId = 2,
            StateHistorySerialized = "[{\"StateId\":1},{\"StateId\":2}]"
        };
        var (_, automaton) = await CreateGroupAndAutomaton(execState, AutomatonSaveMode.WithState);

        // Act
        await controller.Load(automaton.Id, mode: "input");

        // Assert: input loaded but no execution state restored
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
        // Arrange: mode=structure should load only automaton topology
        var execState = new SavedExecutionStateDto { Input = "test", Position = 2, CurrentStateId = 1 };
        var (_, automaton) = await CreateGroupAndAutomaton(execState, AutomatonSaveMode.WithState);

        // Act
        await controller.Load(automaton.Id, mode: "structure");

        // Assert: no state or input loaded
        var model = tempSvc.LastStoredModel;
        model.ShouldNotBeNull();
        model.HasExecuted.ShouldBeFalse();
        model.Input.ShouldBeNullOrEmpty();
        model.Position.ShouldBe(0);
    }

    [Fact]
    public async Task Load_WithModeState_ButEntityHasNoExecutionState_DoesNotSetHasExecuted()
    {
        // Arrange: saved as WithState but ExecutionStateJson is null (e.g. legacy record)
        var (_, automaton) = await CreateGroupAndAutomaton(execState: null, AutomatonSaveMode.WithState);

        // Act
        await controller.Load(automaton.Id, mode: "state");

        // Assert: nothing to restore, so HasExecuted remains false
        var model = tempSvc.LastStoredModel;
        model.ShouldNotBeNull();
        model.HasExecuted.ShouldBeFalse();
    }

    [Fact]
    public async Task Load_WithModeState_ButEntitySavedAsStructureMode_DoesNotSetHasExecuted()
    {
        // Arrange: entity has save mode=Structure, so mode=state request should be ignored
        var execState = new SavedExecutionStateDto { Input = "hi", Position = 1, CurrentStateId = 2 };
        var (_, automaton) = await CreateGroupAndAutomaton(execState, AutomatonSaveMode.Structure);

        // Act
        await controller.Load(automaton.Id, mode: "state");

        // Assert: SaveMode=Structure overrides the mode param — state is NOT loaded
        var model = tempSvc.LastStoredModel;
        model.ShouldNotBeNull();
        model.HasExecuted.ShouldBeFalse();
        model.Position.ShouldBe(0);
    }
}
