using FiniteAutomatons.Core.Models.Api;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FiniteAutomatons.Controllers;

[ApiController]
[Route("api/canvas")]
public class CanvasApiController(ILogger<CanvasApiController> logger) : ControllerBase
{
    private readonly ILogger<CanvasApiController> _logger = logger;

    public const string SessionKey = "CanvasAutomaton";

    [HttpPost("sync")]
    public IActionResult Sync([FromBody] CanvasSyncRequest? request, [FromServices] FiniteAutomatons.Services.Interfaces.IAutomatonMinimizationService? minimizationService)
    {
        if (request is null)
            return BadRequest("Request body is required.");

        try
        {
            var response = BuildResponse(request);
            // Attach minimization analysis when available
            try
            {
                if (minimizationService != null)
                {
                    var vm = BuildViewModel(request);
                    var analysis = minimizationService.AnalyzeAutomaton(vm);
                    response.MinimizationAnalysis = new Core.Models.Api.CanvasMinimizationDto(
                        analysis.SupportsMinimization,
                        analysis.IsMinimal,
                        analysis.OriginalStateCount,
                        analysis.ReachableStateCount,
                        analysis.MinimizedStateCount
                    );
                }
            }
            catch { /* ignore analysis errors */ }
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in canvas sync");
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
            var model = BuildViewModel(request);
            var json = JsonSerializer.Serialize(model);
            HttpContext.Session.SetString(SessionKey, json);
            _logger.LogDebug("Canvas automaton saved to session: Type={Type} States={States}",
                model.Type, model.States.Count);
            return Ok(new { saved = true });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving canvas automaton to session");
            return StatusCode(500, "Internal error during save.");
        }
    }

    [HttpPost("clear")]
    public IActionResult Clear()
    {
        HttpContext.Session.Remove(SessionKey);
        return Ok(new { cleared = true });
    }

    // ──────────────────────────────────────────────────────────────────
    // Private helpers
    // ──────────────────────────────────────────────────────────────────

    private static CanvasSyncResponse BuildResponse(CanvasSyncRequest req)
    {
        bool isPDA = string.Equals(req.Type, "PDA", StringComparison.OrdinalIgnoreCase);

        // Derive alphabet: unique symbols from transitions, excluding epsilon
        var alphabet = req.Transitions
            .Select(t => ParseSymbol(t.Symbol))
            .Where(c => c != '\0')
            .Distinct()
            .OrderBy(c => c)
            .Select(c => c.ToString())
            .ToList();

        bool hasEpsilon = req.Transitions.Any(t => ParseSymbol(t.Symbol) == '\0');

        // Build state DTOs
        var states = req.States
            .OrderBy(s => s.Id)
            .Select(s => new CanvasSyncStateDto
            {
                Id = s.Id,
                IsStart = s.IsStart,
                IsAccepting = s.IsAccepting
            })
            .ToList();

        // Build transition DTOs, sorted for readable display
        var transitions = req.Transitions
            .OrderBy(t => t.FromStateId)
            .ThenBy(t => ParseSymbol(t.Symbol))
            .ThenBy(t => t.ToStateId)
            .Select(t =>
            {
                var sym = ParseSymbol(t.Symbol);
                return new CanvasSyncTransitionDto
                {
                    FromStateId = t.FromStateId,
                    ToStateId = t.ToStateId,
                    SymbolDisplay = sym == '\0' ? "ε" : sym.ToString(),
                    IsPDA = isPDA,
                    StackPopDisplay = isPDA && t.StackPop is not null
                        ? (ParseSymbol(t.StackPop) == '\0' ? "ε" : t.StackPop)
                        : null,
                    StackPush = isPDA ? (t.StackPush ?? "") : null
                };
            })
            .ToList();

        return new CanvasSyncResponse
        {
            Alphabet = alphabet,
            HasEpsilonTransitions = hasEpsilon,
            IsPDA = isPDA,
            StateCount = states.Count,
            TransitionCount = transitions.Count,
            States = states,
            Transitions = transitions
        };
    }

    private static AutomatonViewModel BuildViewModel(CanvasSyncRequest req)
    {
        var type = req.Type?.ToUpperInvariant() switch
        {
            "NFA" => AutomatonType.NFA,
            "EPSILONNFA" => AutomatonType.EpsilonNFA,
            "PDA" => AutomatonType.PDA,
            _ => AutomatonType.DFA
        };

        var states = req.States.Select(s => new State
        {
            Id = s.Id,
            IsStart = s.IsStart,
            IsAccepting = s.IsAccepting
        }).ToList();

        var isPDA = type == AutomatonType.PDA;
        var transitions = req.Transitions.Select(t => new Transition
        {
            FromStateId = t.FromStateId,
            ToStateId = t.ToStateId,
            Symbol = ParseSymbol(t.Symbol),
            StackPop = isPDA ? ParseSymbol(t.StackPop) : null,
            StackPush = isPDA ? (t.StackPush ?? "") : null
        }).ToList();

        return new AutomatonViewModel
        {
            Type = type,
            States = states,
            Transitions = transitions,
            IsCustomAutomaton = true
        };
    }

    private static char ParseSymbol(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return '\0';
        if (raw is "\\0" or "ε" or "epsilon" or "\0") return '\0';
        return raw[0];
    }
}
