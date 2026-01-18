using FiniteAutomatons.Controllers;
using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Services.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.Security.Claims;

namespace FiniteAutomatons.UnitTests.Controllers;

public class AutomatonControllerSavedTests
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
        // IUserStore signature uses non-nullable string return in this target framework
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
        public readonly List<SavedAutomatonGroup> Groups = [];
        public readonly List<SavedAutomaton> Items = [];
        public readonly List<SavedAutomatonGroupMember> Members = [];
        public int NextGroupId = 1;
        public int NextItemId = 1;
        public int NextMemberId = 1;

        public Task<SavedAutomaton> SaveAsync(string userId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, int? groupId = null)
        {
            var entity = new SavedAutomaton { Id = NextItemId++, UserId = userId, Name = name, Description = description, ContentJson = "{}", HasExecutionState = saveExecutionState, ExecutionStateJson = saveExecutionState ? "{}" : null, GroupId = groupId, CreatedAt = DateTime.UtcNow };
            Items.Add(entity);
            return Task.FromResult(entity);
        }

        public Task<List<SavedAutomaton>> ListForUserAsync(string userId, int? groupId = null)
        {
            var q = Items.Where(i => i.UserId == userId);
            if (groupId.HasValue) q = q.Where(i => i.GroupId == groupId.Value);
            return Task.FromResult(q.OrderByDescending(i => i.CreatedAt).ToList());
        }

        public Task<SavedAutomaton?> GetAsync(int id, string userId)
        {
            return Task.FromResult(Items.FirstOrDefault(i => i.Id == id && i.UserId == userId));
        }

        public Task DeleteAsync(int id, string userId)
        {
            var e = Items.FirstOrDefault(i => i.Id == id && i.UserId == userId);
            if (e != null) Items.Remove(e);
            return Task.CompletedTask;
        }

        public Task<SavedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description)
        {
            var g = new SavedAutomatonGroup { Id = NextGroupId++, UserId = userId, Name = name, Description = description };
            Groups.Add(g);
            return Task.FromResult(g);
        }

        public Task<List<SavedAutomatonGroup>> ListGroupsForUserAsync(string userId)
        {
            return Task.FromResult(Groups.Where(g => g.UserId == userId).OrderBy(g => g.Name).ToList());
        }

        // New membership methods
        public Task AddGroupMemberAsync(int groupId, string userId)
        {
            var exists = Members.Any(m => m.GroupId == groupId && m.UserId == userId);
            if (!exists)
            {
                Members.Add(new SavedAutomatonGroupMember { Id = NextMemberId++, GroupId = groupId, UserId = userId });
            }
            return Task.CompletedTask;
        }

        public Task RemoveGroupMemberAsync(int groupId, string userId)
        {
            var m = Members.FirstOrDefault(x => x.GroupId == groupId && x.UserId == userId);
            if (m != null) Members.Remove(m);
            return Task.CompletedTask;
        }

        public Task<List<SavedAutomatonGroupMember>> ListGroupMembersAsync(int groupId)
        {
            return Task.FromResult(Members.Where(m => m.GroupId == groupId).ToList());
        }

        public Task<bool> CanUserSaveToGroupAsync(int groupId, string userId)
        {
            var g = Groups.FirstOrDefault(x => x.Id == groupId);
            if (g == null) return Task.FromResult(false);
            if (g.UserId == userId) return Task.FromResult(true);
            if (!g.MembersCanShare) return Task.FromResult(false);
            var isMember = Members.Any(m => m.GroupId == groupId && m.UserId == userId);
            return Task.FromResult(isMember);
        }

        public Task<SavedAutomatonGroup?> GetGroupAsync(int groupId)
        {
            return Task.FromResult(Groups.FirstOrDefault(g => g.Id == groupId));
        }

        public Task SetGroupSharingPolicyAsync(int groupId, bool membersCanShare)
        {
            var g = Groups.FirstOrDefault(x => x.Id == groupId);
            g?.MembersCanShare = membersCanShare;
            return Task.CompletedTask;
        }

        public Task<List<SavedAutomaton>> ListByGroupAsync(int groupId)
        {
            return Task.FromResult(Items.Where(i => i.GroupId == groupId).OrderByDescending(i => i.CreatedAt).ToList());
        }

        public Task<SavedAutomatonGroup?> GetGroupAsync(int id, string userId)
        {
            return Task.FromResult(Groups.FirstOrDefault(g => g.Id == id && g.UserId == userId));
        }

        public Task DeleteGroupAsync(int id, string userId)
        {
            var g = Groups.FirstOrDefault(i => i.Id == id && i.UserId == userId);
            if (g != null) Groups.Remove(g);
            return Task.CompletedTask;
        }

        public Task<SavedAutomatonGroupMember?> GetGroupMemberAsync(int groupId, string userId)
        {
            return Task.FromResult(Members.FirstOrDefault(m => m.GroupId == groupId && m.UserId == userId));
        }
    }

    private static SavedAutomatonController BuildController(MockSavedAutomatonService svc, IdentityUser user)
    {
        var logger = new NullLogger<AutomatonCreationController>();
        var mockGenerator = new MockAutomatonGeneratorService();
        var tempDataSvc = new MockAutomatonTempDataService();
        var validationSvc = new MockAutomatonValidationService();
        var conversionSvc = new MockAutomatonConversionService();
        var execSvc = new MockAutomatonExecutionService();
        var editingSvc = new AutomatonEditingService(new MockAutomatonValidationService(), new NullLogger<AutomatonEditingService>());
        var fileSvc = new MockAutomatonFileService();

        var userManager = new TestUserManager(user);

        var controller = new SavedAutomatonController(svc, tempDataSvc, userManager);

        var httpContext = new DefaultHttpContext();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        controller.TempData = new TempDataDictionary(httpContext, new TestTempDataProvider());
        // set a principal so GetUserAsync can receive something (our TestUserManager ignores it)
        httpContext.User = new ClaimsPrincipal(new ClaimsIdentity([new Claim(ClaimTypes.NameIdentifier, user.Id)]));

        return controller;
    }

    [Fact]
    public async Task SavedAutomatons_Get_ReturnsViewWithGroupsAndSelectedGroup()
    {
        var svc = new MockSavedAutomatonService();
        var user = new IdentityUser { Id = "u1", UserName = "a@b" };
        var c = BuildController(svc, user);

        var g1 = await svc.CreateGroupAsync(user.Id, "Group1", null);
        var g2 = await svc.CreateGroupAsync(user.Id, "Group2", null);
        await svc.SaveAsync(user.Id, "s1", null, new AutomatonViewModel { Type = AutomatonType.DFA }, false, g1.Id);
        await svc.SaveAsync(user.Id, "s2", null, new AutomatonViewModel { Type = AutomatonType.DFA }, false, g2.Id);

        var result = await c.Index(groupId: g2.Id) as ViewResult; // No change
        result.ShouldNotBeNull();
        result.ViewName.ShouldBe("SavedAutomatons");
        var model = result.Model as List<SavedAutomaton>;
        model.ShouldNotBeNull();
        model.All(i => i.UserId == user.Id).ShouldBeTrue();

        (result.ViewData["Groups"] as List<SavedAutomatonGroup>)!.Count.ShouldBe(2);
        result.ViewData["SelectedGroupId"].ShouldBe(g2.Id);
    }

    [Fact]
    public async Task CreateSavedGroup_Post_EmptyName_ReturnsBadRequest()
    {
        var svc = new MockSavedAutomatonService();
        var user = new IdentityUser { Id = "u2" };
        var c = BuildController(svc, user);

        var res = await c.CreateGroup("   ", null) as BadRequestObjectResult; // No change
        res.ShouldNotBeNull();
    }

    [Fact]
    public async Task CreateSavedGroup_Post_Valid_CreatesAndRedirects()
    {
        var svc = new MockSavedAutomatonService();
        var user = new IdentityUser { Id = "u3" };
        var c = BuildController(svc, user);

        var res = await c.CreateGroup("  mygroup  ", "d") as RedirectToActionResult; // No change
        res.ShouldNotBeNull();
        res.ActionName.ShouldBe("Index");
        // verify created
        var groups = await svc.ListGroupsForUserAsync(user.Id);
        groups.Count.ShouldBe(1);
        groups[0].Name.ShouldBe("mygroup");
    }

    [Fact]
    public async Task SaveAutomaton_Post_NameMissing_ReturnsCreateViewWithModelError()
    {
        var svc = new MockSavedAutomatonService();
        var user = new IdentityUser { Id = "u4" };
        var c = BuildController(svc, user);

        var model = new AutomatonViewModel();
        var result = await c.Save(model, "  ", null, saveState: false) as ViewResult; // No change
        result.ShouldNotBeNull();
        result.ViewName.ShouldBe("CreateAutomaton");
        c.ModelState.IsValid.ShouldBeFalse();
    }

    [Fact]
    public async Task SaveAutomaton_Post_Valid_CallsSaveAndRedirects()
    {
        var svc = new MockSavedAutomatonService();
        var user = new IdentityUser { Id = "u5" };
        var c = BuildController(svc, user);

        var model = new AutomatonViewModel { Type = AutomatonType.DFA };
        var result = await c.Save(model, "name1", "desc", saveState: true) as RedirectToActionResult; // No change
        result.ShouldNotBeNull();
        result.ActionName.ShouldBe("Index");

        var list = await svc.ListForUserAsync(user.Id);
        list.Count.ShouldBe(1);
        list[0].Name.ShouldBe("name1");
        list[0].HasExecutionState.ShouldBeTrue();
    }

    [Fact]
    public async Task LoadSavedAutomaton_Get_LoadsAutomaton_And_LoadsAsState_WhenRequested()
    {
        var svc = new MockSavedAutomatonService();
        var user = new IdentityUser { Id = "u6" };
        var c = BuildController(svc, user);

        // create payload JSON
        var payload = new
        {
            Type = AutomatonType.DFA,
            States = new[] { new { Id = 1, IsStart = true, IsAccepting = false } },
            Transitions = Array.Empty<object>()
        };
        var exec = new
        {
            Input = "xyz",
            Position = 1,
            CurrentStateId = 1,
            CurrentStates = new[] { 1 },
            IsAccepted = true,
            StateHistorySerialized = "h",
            StackSerialized = "s"
        };

        var entity = new SavedAutomaton { Id = 123, UserId = user.Id, ContentJson = System.Text.Json.JsonSerializer.Serialize(payload), HasExecutionState = true, ExecutionStateJson = System.Text.Json.JsonSerializer.Serialize(exec) };

        svc.Items.Add(entity);

        var res = await c.Load(entity.Id, asState: true) as RedirectToActionResult; // No change
        res.ShouldNotBeNull();
        res.ActionName.ShouldBe("Index");
        res.ControllerName.ShouldBe("Home");

        // TempData should contain serialized model
        var td = c.TempData["CustomAutomaton"] as string;
        td.ShouldNotBeNull();
        var model = System.Text.Json.JsonSerializer.Deserialize<AutomatonViewModel>(td!);
        model.ShouldNotBeNull();
        model!.Type.ShouldBe(AutomatonType.DFA);
        model.Input.ShouldBe("xyz");
        model.Position.ShouldBe(1);
        model.CurrentStateId.ShouldBe(1);
        model.IsAccepted.ShouldBe(true);
    }

    [Fact]
    public async Task DeleteSavedAutomaton_Post_DeletesAndRedirects()
    {
        var svc = new MockSavedAutomatonService();
        var user = new IdentityUser { Id = "u7" };
        var c = BuildController(svc, user);

        var e = await svc.SaveAsync(user.Id, "todel", null, new AutomatonViewModel { Type = AutomatonType.DFA }, false);
        var res = await c.Delete(e.Id) as RedirectToActionResult; // No change
        res.ShouldNotBeNull();
        res.ActionName.ShouldBe("Index");

        var fetched = await svc.GetAsync(e.Id, user.Id);
        fetched.ShouldBeNull();
    }
}
