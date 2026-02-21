namespace FiniteAutomatons.Core.Models.Api;

/// <summary>
/// Request body for POST /api/canvas/sync.
/// Sent by the frontend whenever the canvas automaton is edited.
/// </summary>
public class CanvasSyncRequest
{
    /// <summary>"DFA", "NFA", "EpsilonNFA", or "PDA"</summary>
    public string Type { get; set; } = "DFA";

    public List<CanvasSyncState> States { get; set; } = [];
    public List<CanvasSyncTransition> Transitions { get; set; } = [];
}

public class CanvasSyncState
{
    public int Id { get; set; }
    public bool IsStart { get; set; }
    public bool IsAccepting { get; set; }
}

public class CanvasSyncTransition
{
    public int FromStateId { get; set; }
    public int ToStateId { get; set; }

    /// <summary>
    /// Transition symbol. Use "\\0" or "ε" to represent epsilon.
    /// </summary>
    public string Symbol { get; set; } = string.Empty;

    /// <summary>PDA: symbol to pop from stack. Null = no PDA condition. "\\0"/"ε" = don't pop.</summary>
    public string? StackPop { get; set; }

    /// <summary>PDA: string to push onto stack. Null/empty = push nothing.</summary>
    public string? StackPush { get; set; }
}
