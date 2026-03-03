using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.DTOs;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using System.Text.Json;

namespace FiniteAutomatons.Controllers;

using Microsoft.Extensions.DependencyInjection;

[method: ActivatorUtilitiesConstructor]
public class ImportExportController(
    IAutomatonFileService fileService,
    ISavedAutomatonService savedAutomatonService,
    ISharedAutomatonService sharedAutomatonService,
    UserManager<ApplicationUser> userManager) : Controller
{
    private readonly IAutomatonFileService fileService = fileService ?? throw new ArgumentNullException(nameof(fileService));
    private readonly ISavedAutomatonService savedAutomatonService = savedAutomatonService ?? throw new ArgumentNullException(nameof(savedAutomatonService));
    private readonly ISharedAutomatonService sharedAutomatonService = sharedAutomatonService ?? throw new ArgumentNullException(nameof(sharedAutomatonService));
    private readonly UserManager<ApplicationUser> userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));

    internal ImportExportController(IAutomatonFileService fileService, ISavedAutomatonService savedAutomatonService, UserManager<ApplicationUser> userManager)
        : this(fileService, savedAutomatonService, new NoopSharedAutomatonService(), userManager)
    {
    }

    [HttpPost]
    public IActionResult ExportJson([FromForm] AutomatonViewModel model)
    {
        var (name, content) = fileService.ExportJson(model);
        return File(System.Text.Encoding.UTF8.GetBytes(content), "application/json", name);
    }

    [HttpGet]
    public async Task<IActionResult> ExportShared(int id, string format, string mode = "structure")
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var entity = await sharedAutomatonService.GetAsync(id, user.Id);
        if (entity == null) return NotFound();

        var model = JsonSerializer.Deserialize<AutomatonViewModel>(entity.ContentJson);
        if (model == null) return BadRequest("Failed to deserialize automaton");

        fileService.RestoreExecutionState(model, entity.ExecutionStateJson, mode);

        return format switch
        {
            "json" => ExportJsonHelper(model, entity.Name, mode),
            "txt" => ExportTextHelper(model, entity.Name),
            _ => BadRequest("Invalid format")
        };
    }

    [HttpPost]
    public IActionResult ExportJsonWithState([FromForm] AutomatonViewModel model)
    {
        var (name, content) = fileService.ExportJsonWithState(model);
        return File(System.Text.Encoding.UTF8.GetBytes(content), "application/json", name);
    }

    [HttpPost]
    public IActionResult ExportWithInput([FromForm] AutomatonViewModel model)
    {
        var (name, content) = fileService.ExportWithInput(model);
        return File(System.Text.Encoding.UTF8.GetBytes(content), "application/json", name);
    }

    [HttpPost]
    public IActionResult ExportWithExecutionState([FromForm] AutomatonViewModel model)
    {
        var (name, content) = fileService.ExportWithExecutionState(model);
        return File(System.Text.Encoding.UTF8.GetBytes(content), "application/json", name);
    }

    [HttpPost]
    public IActionResult ExportText([FromForm] AutomatonViewModel model)
    {
        var (name, content) = fileService.ExportText(model);
        return File(System.Text.Encoding.UTF8.GetBytes(content), "text/plain", name);
    }

    [HttpGet]
    public async Task<IActionResult> ExportSaved(int id, string format, string mode = "structure")
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var saved = await savedAutomatonService.GetAsync(id, user.Id);
        if (saved == null) return NotFound();

        var model = JsonSerializer.Deserialize<AutomatonViewModel>(saved.ContentJson);
        if (model == null) return BadRequest("Failed to deserialize automaton");

        fileService.RestoreExecutionState(model, saved.ExecutionStateJson, mode);

        return format switch
        {
            "json" => ExportJsonHelper(model, saved.Name, mode),
            "txt" => ExportTextHelper(model, saved.Name),
            _ => BadRequest("Invalid format")
        };
    }

    private FileContentResult ExportJsonHelper(AutomatonViewModel model, string name, string mode)
    {
        string content;
        string fileName;

        switch (mode.ToLowerInvariant())
        {
            case "input":
                (_, content) = fileService.ExportWithInput(model);
                fileName = $"{name}_withinput.json";
                break;
            case "state":
                (_, content) = fileService.ExportWithExecutionState(model);
                fileName = $"{name}_withstate.json";
                break;
            default:
                (_, content) = fileService.ExportJson(model);
                fileName = $"{name}.json";
                break;
        }

        return File(System.Text.Encoding.UTF8.GetBytes(content), "application/json", fileName);
    }

    private FileContentResult ExportTextHelper(AutomatonViewModel model, string name)
    {
        var content = fileService.ExportText(model).Content;
        return File(System.Text.Encoding.UTF8.GetBytes(content), "text/plain", $"{name}.txt");
    }

    [HttpGet]
    public async Task<IActionResult> ExportGroup(int groupId, string v)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // Ensure user can view the group
        var group = await sharedAutomatonService.GetGroupAsync(groupId, user.Id);
        if (group == null) return NotFound();

        var automatons = await sharedAutomatonService.ListForGroupAsync(groupId, user.Id);

        var dto = new GroupExportDto
        {
            GroupName = group.Name,
            GroupDescription = group.Description,
            ExportedAt = DateTime.UtcNow,
            Automatons = []
        };

        foreach (var a in automatons)
        {
            AutomatonPayloadDto? payload = null;
            try { payload = JsonSerializer.Deserialize<AutomatonPayloadDto>(a.ContentJson); } catch { }

            SavedExecutionStateDto? exec = null;
            if (!string.IsNullOrEmpty(a.ExecutionStateJson))
            {
                try { exec = JsonSerializer.Deserialize<SavedExecutionStateDto>(a.ExecutionStateJson); } catch { }
            }

            dto.Automatons.Add(new AutomatonExportItemDto
            {
                Name = a.Name,
                Description = a.Description,
                HasExecutionState = exec != null,
                Content = payload ?? new AutomatonPayloadDto(),
                ExecutionState = exec
            });
        }

        var json = JsonSerializer.Serialize(dto, new JsonSerializerOptions { WriteIndented = true });
        var fileName = $"{SanitizeFileName(group.Name)}_export_{DateTime.UtcNow:yyyyMMddHHmmss}.json";
        return File(System.Text.Encoding.UTF8.GetBytes(json), "application/json", fileName);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ImportGroup(IFormFile upload, int groupId)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        // Ensure TempData is available (tests may not configure a provider)
        if (ControllerContext?.HttpContext != null)
        {
            TempData ??= new TempDataDictionary(ControllerContext.HttpContext, new InMemoryTempDataProvider());
        }

        if (!await sharedAutomatonService.CanUserAddToGroupAsync(groupId, user.Id))
        {
            TempData["Error"] = "You do not have permission to import automatons into this group.";
            return RedirectToAction("Group", "SharedAutomaton", new { id = groupId });
        }

        if (upload == null)
            return BadRequest("No file uploaded");

        using var stream = upload.OpenReadStream();
        // Ensure stream is readable from start
        if (stream.CanSeek) stream.Position = 0;
        GroupExportDto? dto;
        try
        {
            dto = await JsonSerializer.DeserializeAsync<GroupExportDto>(stream);
        }
        catch (Exception ex)
        {
            TempData["Error"] = "Failed to parse group export file: " + ex.Message;
            return RedirectToAction("Group", "SharedAutomaton", new { id = groupId });
        }

        if (dto == null || dto.Automatons == null || dto.Automatons.Count == 0)
        {
            TempData["Error"] = "No automatons found in import file.";
            return RedirectToAction("Group", "SharedAutomaton", new { id = groupId });
        }

        var created = 0;
        foreach (var item in dto.Automatons)
        {
            try
            {
                if (item == null) continue;
                if (item.Content == null)
                {
                    // skip invalid automaton entries
                    continue;
                }
                var model = new AutomatonViewModel
                {
                    Type = item.Content.Type,
                    States = item.Content.States ?? [],
                    Transitions = item.Content.Transitions ?? [],
                    IsCustomAutomaton = true,
                    SourceRegex = null
                };

                if (item.ExecutionState != null)
                {
                    model.Input = item.ExecutionState.Input ?? string.Empty;
                    model.Position = item.ExecutionState.Position;
                    model.CurrentStateId = item.ExecutionState.CurrentStateId;
                    model.CurrentStates = item.ExecutionState.CurrentStates != null ? [.. item.ExecutionState.CurrentStates] : null;
                    model.IsAccepted = item.ExecutionState.IsAccepted;
                    model.StateHistorySerialized = item.ExecutionState.StateHistorySerialized ?? string.Empty;
                    model.StackSerialized = item.ExecutionState.StackSerialized;
                }

                await sharedAutomatonService.SaveAsync(user.Id, groupId, item.Name, item.Description, model, item.HasExecutionState);
                created++;
            }
            catch
            {
                // continue importing remaining automatons
            }
        }

        TempData["CreateGroupResult"] = $"Imported {created} automatons into group.";
        TempData["CreateGroupSuccess"] = "1";
        return RedirectToAction("Group", "SharedAutomaton", new { id = groupId });
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var c in System.IO.Path.GetInvalidFileNameChars())
            name = name.Replace(c, '_');
        return name;
    }

    [HttpPost]
    public async Task<IActionResult> ImportAutomaton(IFormFile upload)
    {
        if (upload == null)
            return BadRequest("No file uploaded");

        var (ok, model, error) = await fileService.LoadViewModelWithStateAsync(upload);
        if (!ok || model == null)
            return BadRequest(error ?? "Failed to load automaton");

        TempData["CustomAutomaton"] = JsonSerializer.Serialize(model);
        return RedirectToAction("Index", "Home");
    }

    private sealed class NoopSharedAutomatonService : ISharedAutomatonService
    {
        public Task<SharedAutomaton> SaveAsync(string userId, int groupId, string name, string? description, AutomatonViewModel model, bool saveExecutionState = false, string? layoutJson = null, string? thumbnailBase64 = null) =>
            Task.FromException<SharedAutomaton>(new NotSupportedException("Noop service"));
        public Task<SharedAutomaton?> GetAsync(int id, string userId) => Task.FromResult<SharedAutomaton?>(null);
        public Task<List<SharedAutomaton>> ListForGroupAsync(int groupId, string userId) => Task.FromResult(new List<SharedAutomaton>());
        public Task<List<SharedAutomaton>> ListForUserAsync(string userId) => Task.FromResult(new List<SharedAutomaton>());
        public Task DeleteAsync(int id, string userId) => Task.CompletedTask;
        public Task<SharedAutomaton> UpdateAsync(int id, string userId, string? name, string? description, AutomatonViewModel? model) => Task.FromException<SharedAutomaton>(new NotSupportedException("Noop service"));
        public Task<SharedAutomatonGroup> CreateGroupAsync(string userId, string name, string? description) => Task.FromException<SharedAutomatonGroup>(new NotSupportedException("Noop service"));
        public Task<SharedAutomatonGroup?> GetGroupAsync(int groupId, string userId) => Task.FromResult<SharedAutomatonGroup?>(null);
        public Task<List<SharedAutomatonGroup>> ListGroupsForUserAsync(string userId) => Task.FromResult(new List<SharedAutomatonGroup>());
        public Task DeleteGroupAsync(int groupId, string userId) => Task.CompletedTask;
        public Task UpdateGroupAsync(int groupId, string userId, string? name, string? description) => Task.CompletedTask;
        public Task<List<SharedAutomatonGroupMember>> ListGroupMembersAsync(int groupId, string userId) => Task.FromResult(new List<SharedAutomatonGroupMember>());
        public Task RemoveMemberAsync(int groupId, string userId, string memberUserId) => Task.CompletedTask;
        public Task UpdateMemberRoleAsync(int groupId, string userId, string memberUserId, SharedGroupRole newRole) => Task.CompletedTask;
        public Task<bool> CanUserViewGroupAsync(int groupId, string userId) => Task.FromResult(false);
        public Task<bool> CanUserAddToGroupAsync(int groupId, string userId) => Task.FromResult(false);
        public Task<bool> CanUserEditInGroupAsync(int groupId, string userId) => Task.FromResult(false);
        public Task<bool> CanUserManageMembersAsync(int groupId, string userId) => Task.FromResult(false);
        public Task<SharedGroupRole?> GetUserRoleInGroupAsync(int groupId, string userId) => Task.FromResult<SharedGroupRole?>(null);
    }

    private sealed class InMemoryTempDataProvider : ITempDataProvider
    {
        private readonly Dictionary<string, object?> data = [];
        public IDictionary<string, object?> LoadTempData(HttpContext context) => new Dictionary<string, object?>(data);
        public void SaveTempData(HttpContext context, IDictionary<string, object?> values)
        {
            data.Clear();
            foreach (var kv in values) data[kv.Key] = kv.Value;
        }
    }
}

