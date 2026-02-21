using FiniteAutomatons.Core.Models.Api;
using Microsoft.AspNetCore.Mvc;

namespace FiniteAutomatons.Controllers;

/// <summary>
/// Stateless API endpoints used by the interactive canvas.
/// No session / TempData — everything is re-computed from the posted data.
/// </summary>
[ApiController]
[Route("api/canvas")]
public class CanvasApiController(ILogger<CanvasApiController> logger) : ControllerBase
{
    private readonly ILogger<CanvasApiController> _logger = logger;

    /// <summary>
    /// Accepts the current automaton state from the canvas editor
    /// and returns derived model info for real-time left-panel updates.
    /// </summary>
    [HttpPost("sync")]
    public IActionResult Sync([FromBody] CanvasSyncRequest? request)
    {
        if (request is null)
            return BadRequest("Request body is required.");

        try
        {
            var response = BuildResponse(request);
            return Ok(response);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error in canvas sync");
            return StatusCode(500, "Internal error during sync.");
        }
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
                    ToStateId   = t.ToStateId,
                    SymbolDisplay = sym == '\0' ? "ε" : sym.ToString(),
                    IsPDA         = isPDA,
                    StackPopDisplay = isPDA && t.StackPop is not null
                        ? (ParseSymbol(t.StackPop) == '\0' ? "ε" : t.StackPop)
                        : null,
                    StackPush = isPDA ? (t.StackPush ?? "") : null
                };
            })
            .ToList();

        return new CanvasSyncResponse
        {
            Alphabet              = alphabet,
            HasEpsilonTransitions = hasEpsilon,
            IsPDA                 = isPDA,
            StateCount            = states.Count,
            TransitionCount       = transitions.Count,
            States                = states,
            Transitions           = transitions
        };
    }

    /// <summary>
    /// Normalize symbol string to a char.
    /// Accepts: "a", "\0", "\\0", "ε", "epsilon" → '\0' for epsilon.
    /// </summary>
    private static char ParseSymbol(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return '\0';
        if (raw is "\\0" or "ε" or "epsilon" or "\0") return '\0';
        return raw[0];
    }
}
