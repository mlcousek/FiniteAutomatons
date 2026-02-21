namespace FiniteAutomatons.Core.Models.Api;

/// <summary>
/// Response body for POST /api/canvas/sync.
/// Contains re-computed model info derived from the submitted automaton state.
/// </summary>
public class CanvasSyncResponse
{
    /// <summary>Derived alphabet symbols (ε excluded, already human-readable)</summary>
    public List<string> Alphabet { get; set; } = [];

    public bool HasEpsilonTransitions { get; set; }
    public bool IsPDA { get; set; }
    public int StateCount { get; set; }
    public int TransitionCount { get; set; }

    public List<CanvasSyncStateDto> States { get; set; } = [];
    public List<CanvasSyncTransitionDto> Transitions { get; set; } = [];
}

public class CanvasSyncStateDto
{
    public int Id { get; set; }
    public bool IsStart { get; set; }
    public bool IsAccepting { get; set; }
    public string Label => $"q{Id}";
}

public class CanvasSyncTransitionDto
{
    public int FromStateId { get; set; }
    public int ToStateId { get; set; }

    /// <summary>Char representation: '\0' stored as 'ε'</summary>
    public string SymbolDisplay { get; set; } = string.Empty;

    // PDA-only
    public string? StackPopDisplay { get; set; }
    public string? StackPush { get; set; }
    public bool IsPDA { get; set; }
}
