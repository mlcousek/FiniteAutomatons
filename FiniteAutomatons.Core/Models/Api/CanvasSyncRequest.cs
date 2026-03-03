namespace FiniteAutomatons.Core.Models.Api;

public class CanvasSyncRequest
{
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

    public string Symbol { get; set; } = string.Empty;

    public string? StackPop { get; set; }

    public string? StackPush { get; set; }
}
