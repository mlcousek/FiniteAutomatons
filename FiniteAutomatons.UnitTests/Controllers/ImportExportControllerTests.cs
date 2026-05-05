using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.DTOs;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Shouldly;
using System.Security.Claims;
using System.Text.Json;

namespace FiniteAutomatons.UnitTests.Controllers;

public class ImportExportControllerTests
{
    private sealed class TestUserStore : IUserStore<ApplicationUser>
    {
        public Task<IdentityResult> CreateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<IdentityResult> DeleteAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public Task<ApplicationUser?> FindByIdAsync(string userId, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
        public Task<ApplicationUser?> FindByNameAsync(string normalizedUserName, CancellationToken cancellationToken) => Task.FromResult<ApplicationUser?>(null);
        public Task<string?> GetNormalizedUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.NormalizedUserName);
        public Task<string> GetUserIdAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(user.Id);
        public Task<string?> GetUserNameAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult<string?>(user.UserName);
        public Task SetNormalizedUserNameAsync(ApplicationUser user, string? normalizedName, CancellationToken cancellationToken) { user.NormalizedUserName = normalizedName; return Task.CompletedTask; }
        public Task SetUserNameAsync(ApplicationUser user, string? userName, CancellationToken cancellationToken) { user.UserName = userName; return Task.CompletedTask; }
        public Task<IdentityResult> UpdateAsync(ApplicationUser user, CancellationToken cancellationToken) => Task.FromResult(IdentityResult.Success);
        public void Dispose() { }
    }

    private sealed class TestUserManager(ApplicationUser user) : UserManager<ApplicationUser>(
        new TestUserStore(),
        new OptionsWrapper<IdentityOptions>(new IdentityOptions()),
        new PasswordHasher<ApplicationUser>(),
        [],
        [],
        new UpperInvariantLookupNormalizer(),
        new IdentityErrorDescriber(),
        null!,
        new Logger<UserManager<ApplicationUser>>(new LoggerFactory()))
    {
        private readonly ApplicationUser user = user;

        public override Task<ApplicationUser?> GetUserAsync(ClaimsPrincipal principal)
        {
            return Task.FromResult<ApplicationUser?>(user);
        }
    }

    private sealed class MockSavedAutomatonService : ISavedAutomatonService
    {
        public List<SavedAutomaton> Items { get; } = [];

        public Task<SavedAutomaton?> GetAsync(int id, string userId)
        {
            return Task.FromResult(Items.FirstOrDefault(i => i.Id == id && i.UserId == userId));
        }

        // Other methods not needed for these tests
        public Task<SavedAutomaton> SaveAsync(string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, int? groupId = null, string? layoutJson = null, string? thumbnailBase64 = null)
            => throw new NotImplementedException();
        public Task<SavedAutomaton> UpdateAsync(int id, string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, string? layoutJson = null, string? thumbnailBase64 = null)
            => throw new NotImplementedException();
        public Task<List<SavedAutomaton>> ListForUserAsync(string userId, int? groupId = null)
            => throw new NotImplementedException();
        public Task DeleteAsync(int id, string userId)
            => throw new NotImplementedException();
        public Task<SavedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description)
            => throw new NotImplementedException();
        public Task<List<SavedAutomatonGroup>> ListGroupsForUserAsync(string userId)
            => throw new NotImplementedException();
        public Task AddGroupMemberAsync(int groupId, string userId)
            => throw new NotImplementedException();
        public Task RemoveGroupMemberAsync(int groupId, string userId)
            => throw new NotImplementedException();
        public Task<List<SavedAutomatonGroupMember>> ListGroupMembersAsync(int groupId)
            => throw new NotImplementedException();
        public Task<bool> CanUserSaveToGroupAsync(int groupId, string userId)
            => throw new NotImplementedException();
        public Task<SavedAutomatonGroup?> GetGroupAsync(int groupId)
            => throw new NotImplementedException();
        public Task SetGroupSharingPolicyAsync(int groupId, bool membersCanShare)
            => throw new NotImplementedException();
        public Task DeleteGroupAsync(int groupId, string userId)
            => throw new NotImplementedException();
        public Task AssignAutomatonToGroupAsync(int automatonId, string userId, int? groupId)
            => throw new NotImplementedException();
        public Task RemoveAutomatonFromGroupAsync(int automatonId, string userId, int groupId)
            => throw new NotImplementedException();
    }

    private static ImportExportController BuildController(MockSavedAutomatonService savedSvc, IAutomatonFileService fileSvc, ApplicationUser user)
    {
        var userManager = new TestUserManager(user);
        // Use a fake shared service for controller construction to match production constructor
        var sharedSvc = new FakeSharedAutomatonService();
        var controller = new ImportExportController(fileSvc, savedSvc, sharedSvc, userManager);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)]));

        return controller;
    }

    private static ImportExportController BuildControllerWithShared(MockSavedAutomatonService savedSvc, IAutomatonFileService fileSvc, ISharedAutomatonService sharedSvc, ApplicationUser user)
    {
        var userManager = new TestUserManager(user);
        var controller = new ImportExportController(fileSvc, savedSvc, sharedSvc, userManager);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)]));

        return controller;
    }

    private sealed class FakeSharedAutomatonService : ISharedAutomatonService
    {
        public readonly List<SharedAutomaton> Saved = [];
        public readonly List<(string userId, int groupId, string name)> Created = [];

        public Task<SharedAutomaton> SaveAsync(string userId, int groupId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, string? layoutJson = null, string? thumbnailBase64 = null)
        {
            var a = new SharedAutomaton { Id = Saved.Count + 1, CreatedByUserId = userId, Name = name, Description = description, ContentJson = JsonSerializer.Serialize(new { }), CreatedAt = DateTime.UtcNow };
            Saved.Add(a);
            Created.Add((userId, groupId, name));
            return Task.FromResult(a);
        }

        public Task<SharedAutomaton?> GetAsync(int id, string userId) => Task.FromResult(Saved.FirstOrDefault(s => s.Id == id));
        public Task<List<SharedAutomaton>> ListForGroupAsync(int groupId, string userId) => Task.FromResult(Saved.ToList());
        public Task<List<SharedAutomaton>> ListForUserAsync(string userId) => Task.FromResult(new List<SharedAutomaton>());
        public Task DeleteAsync(int id, string userId) { var item = Saved.FirstOrDefault(s => s.Id == id); if (item != null) Saved.Remove(item); return Task.CompletedTask; }
        public Task<SharedAutomaton> UpdateAsync(int id, string userId, string? name, string? description, AutomatonViewModel? model) => Task.FromException<SharedAutomaton>(new NotSupportedException());

        public Task<SharedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description) => Task.FromException<SharedAutomatonGroup>(new NotSupportedException());
        public Task<SharedAutomatonGroup?> GetGroupAsync(int groupId, string userId)
            => Task.FromResult<SharedAutomatonGroup?>(new SharedAutomatonGroup { Id = groupId, Name = "TestGroup", Description = "desc", UserId = userId });
        public Task<List<SharedAutomatonGroup>> ListGroupsForUserAsync(string userId) => Task.FromResult(new List<SharedAutomatonGroup>());
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

    #region ExportSaved - Structure Mode Tests

    [Fact]
    public async Task ExportSaved_StructureMode_ExportsOnlyStructure()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "user-export-1" };
        var controller = BuildController(savedSvc, fileSvc, user);

        var payload = new
        {
            Type = AutomatonType.DFA,
            States = new[] { new { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = new[] { new { FromStateId = 1, ToStateId = 1, Symbol = 'a' } }
        };

        var execState = new
        {
            Input = "test",
            Position = 2,
            CurrentStateId = 1,
            IsAccepted = true
        };

        var automaton = new SavedAutomaton
        {
            Id = 1,
            UserId = user.Id,
            Name = "TestAuto",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.WithState,
            ExecutionStateJson = JsonSerializer.Serialize(execState)
        };

        savedSvc.Items.Add(automaton);

        var result = await controller.ExportSaved(1, "json", "structure") as FileContentResult;

        result.ShouldNotBeNull();
        result.ContentType.ShouldBe("application/json");
        result.FileDownloadName.ShouldBe("TestAuto.json");

        var content = System.Text.Encoding.UTF8.GetString(result.FileContents);
        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);

        exported.ShouldNotBeNull();
        exported!.Type.ShouldBe(AutomatonType.DFA);
        exported.States.Count.ShouldBe(1);
        exported.Transitions.Count.ShouldBe(1);
        exported.Input.ShouldBe(string.Empty);
        exported.Position.ShouldBe(0);
        exported.CurrentStateId.ShouldBeNull();
    }

    [Fact]
    public async Task ExportSaved_StructureMode_TextFormat_ExportsAsText()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "user-export-2" };
        var controller = BuildController(savedSvc, fileSvc, user);

        var payload = new
        {
            Type = AutomatonType.DFA,
            States = new[] { new { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = Array.Empty<object>()
        };

        var automaton = new SavedAutomaton
        {
            Id = 2,
            UserId = user.Id,
            Name = "TextExport",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.Structure
        };

        savedSvc.Items.Add(automaton);

        var result = await controller.ExportSaved(2, "txt", "structure") as FileContentResult;

        result.ShouldNotBeNull();
        result.ContentType.ShouldBe("text/plain");
        result.FileDownloadName.ShouldBe("TextExport.txt");
    }

    #endregion

    #region ExportGroup / ImportGroup Controller Tests

    [Fact]
    public async Task ExportGroup_Controller_ReturnsGroupJsonFile()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "group-user-1" };

        var sharedSvc = new FakeSharedAutomatonService();
        // seed one automaton
        sharedSvc.Saved.Add(new SharedAutomaton
        {
            Id = 1,
            Name = "GAuto",
            Description = "desc",
            ContentJson = JsonSerializer.Serialize(new { Type = AutomatonType.DFA, States = Array.Empty<object>(), Transitions = Array.Empty<object>() }),
            SaveMode = AutomatonSaveMode.Structure,
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        });

        var controller = BuildControllerWithShared(savedSvc, fileSvc, sharedSvc, user);

        var result = await controller.ExportGroup(1, "json");

        var fileResult = result.ShouldBeOfType<FileContentResult>();
        fileResult.ContentType.ShouldBe("application/json");
        fileResult.FileDownloadName.ShouldNotBeNull();

        var content = System.Text.Encoding.UTF8.GetString(fileResult.FileContents);
        var dto = JsonSerializer.Deserialize<GroupExportDto>(content);
        dto.ShouldNotBeNull();
        dto!.GroupName.ShouldBe("TestGroup"); // FakeSharedAutomatonService returns TestGroup
        dto.Automatons.Count.ShouldBe(1);
        dto.Automatons[0].Name.ShouldBe("GAuto");
    }

    [Fact]
    public async Task ImportGroup_Controller_ValidFile_ImportsAutomatons()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "group-user-2" };

        var sharedSvc = new FakeSharedAutomatonService();

        var dto = new GroupExportDto
        {
            GroupName = "ImportGroup",
            GroupDescription = "desc",
            ExportedAt = DateTime.UtcNow,
            Automatons =
            [
                new AutomatonExportItemDto
                {
                    Name = "Imported1",
                    Description = "d1",
                    HasExecutionState = false,
                    Content = new AutomatonPayloadDto { Type = AutomatonType.DFA, States = [], Transitions = [] }
                },
                new AutomatonExportItemDto
                {
                    Name = "Imported2",
                    Description = "d2",
                    HasExecutionState = false,
                    Content = new AutomatonPayloadDto { Type = AutomatonType.NFA, States = [], Transitions = [] }
                }
            ]
        };

        var json = JsonSerializer.Serialize(dto);
        var bytes = System.Text.Encoding.UTF8.GetBytes(json);
        using var ms = new System.IO.MemoryStream(bytes);
        var formFile = new FormFile(ms, 0, bytes.Length, "upload", "group.json") { Headers = new HeaderDictionary(), ContentType = "application/json" };

        var controller = BuildControllerWithShared(savedSvc, fileSvc, sharedSvc, user);

        var result = await controller.ImportGroup(formFile, 1);

        var redirect = result.ShouldBeOfType<RedirectToActionResult>();
        redirect.ActionName.ShouldBe("Group");
        // two automatons created
        sharedSvc.Created.Count.ShouldBe(2);
        sharedSvc.Created[0].name.ShouldBe("Imported1");
        sharedSvc.Created[1].name.ShouldBe("Imported2");
    }

    #endregion

    #region ExportSaved - Input Mode Tests

    [Fact]
    public async Task ExportSaved_InputMode_ExportsStructureAndInput()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "user-export-3" };
        var controller = BuildController(savedSvc, fileSvc, user);

        var payload = new
        {
            Type = AutomatonType.DFA,
            States = new[] { new { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = Array.Empty<object>()
        };

        var execState = new
        {
            Input = "abc123",
            Position = 3,
            CurrentStateId = 1,
            IsAccepted = true,
            StateHistorySerialized = "[1,1,1]"
        };

        var automaton = new SavedAutomaton
        {
            Id = 3,
            UserId = user.Id,
            Name = "InputTest",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.WithInput,
            ExecutionStateJson = JsonSerializer.Serialize(execState)
        };

        savedSvc.Items.Add(automaton);

        var result = await controller.ExportSaved(3, "json", "input") as FileContentResult;

        result.ShouldNotBeNull();
        result.FileDownloadName.ShouldBe("InputTest_withinput.json");

        var content = System.Text.Encoding.UTF8.GetString(result.FileContents);
        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);

        exported.ShouldNotBeNull();
        exported!.Input.ShouldBe("abc123");
        exported.Position.ShouldBe(0);
        exported.CurrentStateId.ShouldBeNull();
        exported.IsAccepted.ShouldBeNull();
        exported.StateHistorySerialized.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ExportSaved_InputMode_WithoutExecutionState_ExportsEmptyInput()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "user-export-4" };
        var controller = BuildController(savedSvc, fileSvc, user);

        var payload = new
        {
            Type = AutomatonType.DFA,
            States = new[] { new { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = Array.Empty<object>()
        };

        var automaton = new SavedAutomaton
        {
            Id = 4,
            UserId = user.Id,
            Name = "NoInput",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.Structure
        };

        savedSvc.Items.Add(automaton);

        var result = await controller.ExportSaved(4, "json", "input") as FileContentResult;

        result.ShouldNotBeNull();
        var content = System.Text.Encoding.UTF8.GetString(result.FileContents);
        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);

        exported.ShouldNotBeNull();
        exported!.Input.ShouldBe(string.Empty);
    }

    #endregion

    #region ExportSaved - State Mode Tests

    [Fact]
    public async Task ExportSaved_StateMode_ExportsFullExecutionState()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "user-export-5" };
        var controller = BuildController(savedSvc, fileSvc, user);

        var payload = new
        {
            Type = AutomatonType.NFA,
            States = new[] {
                new { Id = 1, IsStart = true, IsAccepting = false },
                new { Id = 2, IsStart = false, IsAccepting = true }
            },
            Transitions = new[] { new { FromStateId = 1, ToStateId = 2, Symbol = 'a' } }
        };

        var execState = new
        {
            Input = "aaa",
            Position = 2,
            CurrentStateId = 2,
            IsAccepted = false,
            StateHistorySerialized = "[1,2]",
            StackSerialized = (string?)null
        };

        var automaton = new SavedAutomaton
        {
            Id = 5,
            UserId = user.Id,
            Name = "StateTest",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.WithState,
            ExecutionStateJson = JsonSerializer.Serialize(execState)
        };

        savedSvc.Items.Add(automaton);

        var result = await controller.ExportSaved(5, "json", "state") as FileContentResult;

        result.ShouldNotBeNull();
        result.FileDownloadName.ShouldBe("StateTest_withstate.json");

        var content = System.Text.Encoding.UTF8.GetString(result.FileContents);
        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);

        exported.ShouldNotBeNull();
        exported!.Input.ShouldBe("aaa");
        exported.Position.ShouldBe(2);
        exported.CurrentStateId.ShouldBe(2);
        exported.IsAccepted.ShouldBe(false);
        exported.StateHistorySerialized.ShouldBe("[1,2]");
    }

    [Fact]
    public async Task ExportSaved_StateMode_PDA_ExportsStackState()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "user-export-6" };
        var controller = BuildController(savedSvc, fileSvc, user);

        var payload = new
        {
            Type = AutomatonType.DPDA,
            States = new[] { new { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = Array.Empty<object>()
        };

        var execState = new
        {
            Input = "test",
            Position = 1,
            CurrentStateId = 1,
            IsAccepted = (bool?)null,
            StateHistorySerialized = "[1]",
            StackSerialized = "#ABC"
        };

        var automaton = new SavedAutomaton
        {
            Id = 6,
            UserId = user.Id,
            Name = "PDAState",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.WithState,
            ExecutionStateJson = JsonSerializer.Serialize(execState)
        };

        savedSvc.Items.Add(automaton);

        var result = await controller.ExportSaved(6, "json", "state") as FileContentResult;

        result.ShouldNotBeNull();
        var content = System.Text.Encoding.UTF8.GetString(result.FileContents);
        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);

        exported.ShouldNotBeNull();
        exported!.StackSerialized.ShouldBe("#ABC");
        exported.StateHistorySerialized.ShouldBe("[1]");
    }

    [Fact]
    public async Task ExportSaved_StateMode_WithoutExecutionState_ExportsStructureOnly()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "user-export-7" };
        var controller = BuildController(savedSvc, fileSvc, user);

        var payload = new
        {
            Type = AutomatonType.DFA,
            States = new[] { new { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = Array.Empty<object>()
        };

        var automaton = new SavedAutomaton
        {
            Id = 7,
            UserId = user.Id,
            Name = "NoState",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.Structure
        };

        savedSvc.Items.Add(automaton);

        var result = await controller.ExportSaved(7, "json", "state") as FileContentResult;

        result.ShouldNotBeNull();
        var content = System.Text.Encoding.UTF8.GetString(result.FileContents);
        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);

        exported.ShouldNotBeNull();
        exported!.Input.ShouldBe(string.Empty);
        exported.Position.ShouldBe(0);
        exported.CurrentStateId.ShouldBeNull();
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public async Task ExportSaved_NonExistentAutomaton_ReturnsNotFound()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "user-export-8" };
        var controller = BuildController(savedSvc, fileSvc, user);

        var result = await controller.ExportSaved(999, "json", "structure");

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ExportSaved_WrongUser_ReturnsNotFound()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "user-export-9" };
        var controller = BuildController(savedSvc, fileSvc, user);

        var payload = new
        {
            Type = AutomatonType.DFA,
            States = new[] { new { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = Array.Empty<object>()
        };

        var automaton = new SavedAutomaton
        {
            Id = 10,
            UserId = "other-user",
            Name = "OtherUser",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.Structure
        };

        savedSvc.Items.Add(automaton);

        var result = await controller.ExportSaved(10, "json", "structure");

        result.ShouldBeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task ExportSaved_InvalidFormat_ReturnsBadRequest()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "user-export-10" };
        var controller = BuildController(savedSvc, fileSvc, user);

        var payload = new
        {
            Type = AutomatonType.DFA,
            States = new[] { new { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = Array.Empty<object>()
        };

        var automaton = new SavedAutomaton
        {
            Id = 11,
            UserId = user.Id,
            Name = "Test",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.Structure
        };

        savedSvc.Items.Add(automaton);

        var result = await controller.ExportSaved(11, "invalid", "structure") as BadRequestObjectResult;

        result.ShouldNotBeNull();
        result.Value.ShouldBe("Invalid format");
    }

    [Fact]
    public async Task ExportSaved_DefaultMode_UsesStructure()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "user-export-11" };
        var controller = BuildController(savedSvc, fileSvc, user);

        var payload = new
        {
            Type = AutomatonType.DFA,
            States = new[] { new { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = Array.Empty<object>()
        };

        var execState = new
        {
            Input = "test",
            Position = 1,
            CurrentStateId = 1
        };

        var automaton = new SavedAutomaton
        {
            Id = 12,
            UserId = user.Id,
            Name = "DefaultMode",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.WithInput,
            ExecutionStateJson = JsonSerializer.Serialize(execState)
        };

        savedSvc.Items.Add(automaton);

        // Call without mode parameter (defaults to "structure")
        var result = await controller.ExportSaved(12, "json") as FileContentResult;

        result.ShouldNotBeNull();
        var content = System.Text.Encoding.UTF8.GetString(result.FileContents);
        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);

        exported.ShouldNotBeNull();
        exported!.Input.ShouldBe(string.Empty);
        exported.Position.ShouldBe(0);
    }

    #endregion

    #region File Naming Tests

    [Fact]
    public async Task ExportSaved_StructureMode_CorrectFileName()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "user-name-1" };
        var controller = BuildController(savedSvc, fileSvc, user);

        var payload = new
        {
            Type = AutomatonType.DFA,
            States = new[] { new { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = Array.Empty<object>()
        };

        var automaton = new SavedAutomaton
        {
            Id = 13,
            UserId = user.Id,
            Name = "MyAutomaton",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.Structure
        };

        savedSvc.Items.Add(automaton);

        var result = await controller.ExportSaved(13, "json", "structure") as FileContentResult;

        result.ShouldNotBeNull();
        result.FileDownloadName.ShouldBe("MyAutomaton.json");
    }

    [Fact]
    public async Task ExportSaved_InputMode_CorrectFileName()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "user-name-2" };
        var controller = BuildController(savedSvc, fileSvc, user);

        var payload = new
        {
            Type = AutomatonType.DFA,
            States = new[] { new { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = Array.Empty<object>()
        };

        var automaton = new SavedAutomaton
        {
            Id = 14,
            UserId = user.Id,
            Name = "InputAutomaton",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.WithInput
        };

        savedSvc.Items.Add(automaton);

        var result = await controller.ExportSaved(14, "json", "input") as FileContentResult;

        result.ShouldNotBeNull();
        result.FileDownloadName.ShouldBe("InputAutomaton_withinput.json");
    }

    [Fact]
    public async Task ExportSaved_StateMode_CorrectFileName()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "user-name-3" };
        var controller = BuildController(savedSvc, fileSvc, user);

        var payload = new
        {
            Type = AutomatonType.DFA,
            States = new[] { new { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = Array.Empty<object>()
        };

        var automaton = new SavedAutomaton
        {
            Id = 15,
            UserId = user.Id,
            Name = "StateAutomaton",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.WithState
        };

        savedSvc.Items.Add(automaton);

        var result = await controller.ExportSaved(15, "json", "state") as FileContentResult;

        result.ShouldNotBeNull();
        result.FileDownloadName.ShouldBe("StateAutomaton_withstate.json");
    }

    #endregion

    #region Integration with SaveMode Tests

    [Fact]
    public async Task ExportSaved_SaveModeStructure_InputMode_ReturnsEmptyInput()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "user-mode-1" };
        var controller = BuildController(savedSvc, fileSvc, user);

        var payload = new
        {
            Type = AutomatonType.DFA,
            States = new[] { new { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = Array.Empty<object>()
        };

        var automaton = new SavedAutomaton
        {
            Id = 16,
            UserId = user.Id,
            Name = "StructureOnly",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.Structure,
            ExecutionStateJson = null
        };

        savedSvc.Items.Add(automaton);

        var result = await controller.ExportSaved(16, "json", "input") as FileContentResult;

        result.ShouldNotBeNull();
        var content = System.Text.Encoding.UTF8.GetString(result.FileContents);
        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);

        exported.ShouldNotBeNull();
        exported!.Input.ShouldBe(string.Empty);
    }

    [Fact]
    public async Task ExportSaved_SaveModeWithInput_StateMode_ClearsExecutionState()
    {
        var savedSvc = new MockSavedAutomatonService();
        var fileSvc = new MockAutomatonFileService();
        var user = new ApplicationUser { Id = "user-mode-2" };
        var controller = BuildController(savedSvc, fileSvc, user);

        var payload = new
        {
            Type = AutomatonType.DFA,
            States = new[] { new { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = Array.Empty<object>()
        };

        var execState = new
        {
            Input = "abc",
            Position = 0,
            CurrentStateId = (int?)null,
            IsAccepted = (bool?)null,
            StateHistorySerialized = string.Empty
        };

        var automaton = new SavedAutomaton
        {
            Id = 17,
            UserId = user.Id,
            Name = "WithInput",
            ContentJson = JsonSerializer.Serialize(payload),
            SaveMode = AutomatonSaveMode.WithInput,
            ExecutionStateJson = JsonSerializer.Serialize(execState)
        };

        savedSvc.Items.Add(automaton);

        var result = await controller.ExportSaved(17, "json", "state") as FileContentResult;

        result.ShouldNotBeNull();
        var content = System.Text.Encoding.UTF8.GetString(result.FileContents);
        var exported = JsonSerializer.Deserialize<AutomatonViewModel>(content);

        exported.ShouldNotBeNull();
        exported!.Input.ShouldBe("abc");
        exported.Position.ShouldBe(0);
        exported.CurrentStateId.ShouldBeNull();
    }

    #endregion
}

