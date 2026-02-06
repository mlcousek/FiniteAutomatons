using FiniteAutomatons.Core.Models.Database;
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
    public async Task<IActionResult> ExportSaved(int id, string format, string mode = "structure")
    {
        var user = await userManager.GetUserAsync(User);
        if (user == null) return Challenge();

        var saved = await savedAutomatonService.GetAsync(id, user.Id);
        if (saved == null) return NotFound();

        // Deserialize the saved automaton JSON
        var model = JsonSerializer.Deserialize<AutomatonViewModel>(saved.ContentJson);
        if (model == null) return BadRequest("Failed to deserialize automaton");

        // Apply mode-specific logic
        switch (mode.ToLowerInvariant())
        {
            case "input":
                // Include input but clear execution state
                if (!string.IsNullOrEmpty(saved.ExecutionStateJson))
                {
                    var execState = JsonSerializer.Deserialize<JsonElement>(saved.ExecutionStateJson);
                    if (execState.ValueKind != JsonValueKind.Undefined && execState.TryGetProperty("Input", out var input))
                    {
                        model.Input = input.GetString() ?? string.Empty;
                    }
                }
                // Clear execution state
                model.Position = 0;
                model.CurrentStateId = null;
                model.CurrentStates = null;
                model.IsAccepted = null;
                model.StateHistorySerialized = string.Empty;
                model.StackSerialized = null;
                break;

            case "state":
                // Include full execution state if available
                if (!string.IsNullOrEmpty(saved.ExecutionStateJson))
                {
                    var execState = JsonSerializer.Deserialize<JsonElement>(saved.ExecutionStateJson);
                    if (execState.ValueKind != JsonValueKind.Undefined)
                    {
                        if (execState.TryGetProperty("Input", out var input)) model.Input = input.GetString() ?? string.Empty;
                        if (execState.TryGetProperty("Position", out var pos)) model.Position = pos.GetInt32();
                        if (execState.TryGetProperty("CurrentStateId", out var csid) && csid.ValueKind != JsonValueKind.Null) 
                            model.CurrentStateId = csid.GetInt32();
                        if (execState.TryGetProperty("IsAccepted", out var acc) && acc.ValueKind != JsonValueKind.Null)
                            model.IsAccepted = acc.GetBoolean();
                        if (execState.TryGetProperty("StateHistorySerialized", out var hist)) 
                            model.StateHistorySerialized = hist.GetString() ?? string.Empty;
                        if (execState.TryGetProperty("StackSerialized", out var stack) && stack.ValueKind != JsonValueKind.Null) 
                            model.StackSerialized = stack.GetString();
                    }
                }
                break;

            case "structure":
            default:
                // Structure only - no input, no execution state
                model.Input = string.Empty;
                model.Position = 0;
                model.CurrentStateId = null;
                model.CurrentStates = null;
                model.IsAccepted = null;
                model.StateHistorySerialized = string.Empty;
                model.StackSerialized = null;
                break;
        }

        // Export based on format
        return format switch
        {
            "json" => ExportJsonHelper(model, saved.Name, mode),
            "txt" => ExportTextHelper(model, saved.Name),
            _ => BadRequest("Invalid format")
        };
    }

    private IActionResult ExportJsonHelper(AutomatonViewModel model, string name, string mode)
    {
        string content;
        string fileName;

        switch (mode.ToLowerInvariant())
        {
            case "input":
                (fileName, content) = fileService.ExportWithInput(model);
                fileName = $"{name}_withinput.json";
                break;
            case "state":
                (fileName, content) = fileService.ExportWithExecutionState(model);
                fileName = $"{name}_withstate.json";
                break;
            default:
                (fileName, content) = fileService.ExportJson(model);
                fileName = $"{name}.json";
                break;
        }

        return File(System.Text.Encoding.UTF8.GetBytes(content), "application/json", fileName);
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

