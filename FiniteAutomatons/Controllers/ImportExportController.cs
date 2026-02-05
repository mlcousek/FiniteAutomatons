using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FiniteAutomatons.Controllers;

public class ImportExportController(
    IAutomatonFileService fileService,
    ISavedAutomatonService savedAutomatonService,
    UserManager<IdentityUser> userManager) : Controller
{
    private readonly IAutomatonFileService fileService = fileService;
    private readonly ISavedAutomatonService savedAutomatonService = savedAutomatonService;
    private readonly UserManager<IdentityUser> userManager = userManager;

    [HttpPost]
    public IActionResult ExportJson([FromForm] AutomatonViewModel model)
    {
        var (name, content) = fileService.ExportJson(model);
        return File(System.Text.Encoding.UTF8.GetBytes(content), "application/json", name);
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
    public async Task<IActionResult> ExportSaved(int id, string format)
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var saved = await savedAutomatonService.GetAsync(id, user.Id);
        if (saved == null) return NotFound();

        // Deserialize the saved automaton JSON
        var model = JsonSerializer.Deserialize<AutomatonViewModel>(saved.ContentJson);
        if (model == null) return BadRequest("Failed to deserialize automaton");

        // If there's execution state and format is json, include it
        if (saved.HasExecutionState && !string.IsNullOrEmpty(saved.ExecutionStateJson) && format == "json")
        {
            var execState = JsonSerializer.Deserialize<JsonElement>(saved.ExecutionStateJson);
            if (execState.ValueKind != JsonValueKind.Undefined)
            {
                if (execState.TryGetProperty("Input", out var input)) model.Input = input.GetString() ?? string.Empty;
                if (execState.TryGetProperty("Position", out var pos)) model.Position = pos.GetInt32();
                if (execState.TryGetProperty("CurrentStateId", out var csid)) model.CurrentStateId = csid.GetInt32();
                if (execState.TryGetProperty("IsAccepted", out var acc)) model.IsAccepted = acc.GetBoolean();
                if (execState.TryGetProperty("StateHistorySerialized", out var hist)) model.StateHistorySerialized = hist.GetString() ?? string.Empty;
                if (execState.TryGetProperty("StackSerialized", out var stack)) model.StackSerialized = stack.GetString();
            }
        }

        // Export based on format
        return format switch
        {
            "json" => ExportJsonHelper(model, saved.Name),
            "txt" => ExportTextHelper(model, saved.Name),
            _ => BadRequest("Invalid format")
        };
    }

    private IActionResult ExportJsonHelper(AutomatonViewModel model, string name)
    {
        var content = fileService.ExportJson(model).Content;
        return File(System.Text.Encoding.UTF8.GetBytes(content), "application/json", $"{name}.json");
    }

    private IActionResult ExportTextHelper(AutomatonViewModel model, string name)
    {
        var content = fileService.ExportText(model).Content;
        return File(System.Text.Encoding.UTF8.GetBytes(content), "text/plain", $"{name}.txt");
    }

    [HttpPost]
    public async Task<IActionResult> ImportAutomaton(IFormFile upload)
    {
        if (upload == null)
            return BadRequest("No file uploaded");

        // Single entry point: try to load a full view-model (with execution state) first,
        // fallback to domain-only parsing inside the service. The service encapsulates
        // detection and parsing logic so the controller remains thin.
        var (ok, model, error) = await fileService.LoadViewModelWithStateAsync(upload);
        if (!ok || model == null)
            return BadRequest(error ?? "Failed to load automaton");

        TempData["CustomAutomaton"] = JsonSerializer.Serialize(model);
        return RedirectToAction("Index", "Home");
    }
}

