using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;

namespace FiniteAutomatons.Controllers;

public class ImportExportController(IFormFile file, IAutomatonFileService fileService) : Controller
{
    private readonly IAutomatonFileService fileService = fileService;

    [HttpPost]
    public IActionResult ExportJson([FromForm] AutomatonViewModel model)
    {
        var (name, content) = fileService.ExportJson(model);
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
        {
            return BadRequest("No file uploaded");
        }
        var (ok, model, error) = await fileService.LoadFromFileAsync(upload);
        if (!ok || model == null)
        {
            return BadRequest(error ?? "Failed to load automaton");
        }
        // store in temp data handled by caller (AutomatonController) - redirect to Home is fine
        TempData["CustomAutomaton"] = System.Text.Json.JsonSerializer.Serialize(model);
        return RedirectToAction("Index", "Home");
    }
}
