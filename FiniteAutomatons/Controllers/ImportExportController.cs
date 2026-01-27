using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FiniteAutomatons.Controllers;

public class ImportExportController(IAutomatonFileService fileService) : Controller
{
    private readonly IAutomatonFileService fileService = fileService;

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
    public IActionResult ExportText([FromForm] AutomatonViewModel model)
    {
        var (name, content) = fileService.ExportText(model);
        return File(System.Text.Encoding.UTF8.GetBytes(content), "text/plain", name);
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

        TempData["CustomAutomaton"] = System.Text.Json.JsonSerializer.Serialize(model);
        return RedirectToAction("Index", "Home");
    }
}
