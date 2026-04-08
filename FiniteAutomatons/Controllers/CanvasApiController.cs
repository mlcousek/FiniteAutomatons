using FiniteAutomatons.Core.Models.Api;
using FiniteAutomatons.Services.Services;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FiniteAutomatons.Controllers;

[ApiController]
[Route("api/canvas")]
public class CanvasApiController(
    ILogger<CanvasApiController> logger, 
    ICanvasMappingService canvasMappingService,
    IAutomatonMinimizationService minimizationService) : ControllerBase
{
    private readonly ILogger<CanvasApiController> logger = logger;
    private readonly ICanvasMappingService canvasMappingService = canvasMappingService;
    private readonly IAutomatonMinimizationService minimizationService = minimizationService;

    public const string SessionKey = "CanvasAutomaton";

    [HttpPost("sync")]
    public IActionResult Sync([FromBody] CanvasSyncRequest? request)
    {
        if (request is null)
            return BadRequest("Request body is required.");

        try
        {
            var vm = canvasMappingService.BuildAutomatonViewModel(request);
            var determinismError = DeterminismValidationHelper.GetDeterminismError(vm);
            if (!string.IsNullOrWhiteSpace(determinismError))
            {
                return Conflict(determinismError);
            }

            var response = canvasMappingService.BuildSyncResponse(request);

            try
            {
                var analysis = minimizationService.AnalyzeAutomaton(vm);
                response.MinimizationAnalysis = new CanvasMinimizationDto(
                    analysis.SupportsMinimization,
                    analysis.IsMinimal,
                    analysis.OriginalStateCount,
                    analysis.ReachableStateCount,
                    analysis.MinimizedStateCount
                );
            }
            catch { /* ignore analysis errors */ }
            return Ok(response);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in canvas sync");
            return StatusCode(500, "Internal error during sync.");
        }
    }

    [HttpPost("save")]
    public IActionResult Save([FromBody] CanvasSyncRequest? request)
    {
        if (request is null)
            return BadRequest("Request body is required.");

        try
        {
            var model = canvasMappingService.BuildAutomatonViewModel(request);
            var determinismError = DeterminismValidationHelper.GetDeterminismError(model);
            if (!string.IsNullOrWhiteSpace(determinismError))
            {
                return Conflict(determinismError);
            }

            var json = JsonSerializer.Serialize(model);
            HttpContext.Session.SetString(SessionKey, json);
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Canvas automaton saved to session: Type={Type} States={States}",
                model.Type, model.States.Count);
            }
            return Ok(new { saved = true });
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error saving canvas automaton to session");
            return StatusCode(500, "Internal error during save.");
        }
    }

    [HttpPost("clear")]
    public IActionResult Clear()
    {
        HttpContext.Session.Remove(SessionKey);
        return Ok(new { cleared = true });
    }
}
