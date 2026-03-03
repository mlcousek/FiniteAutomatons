namespace FiniteAutomatons.Core.Models.Api;

public class CanvasSyncResponse
{
    public List<string> Alphabet { get; set; } = [];

    public bool HasEpsilonTransitions { get; set; }
    public bool IsPDA { get; set; }
    public int StateCount { get; set; }
    public int TransitionCount { get; set; }

    public List<CanvasSyncStateDto> States { get; set; } = [];
    public List<CanvasSyncTransitionDto> Transitions { get; set; } = [];

    public CanvasMinimizationDto? MinimizationAnalysis { get; set; }
}

public sealed record CanvasMinimizationDto(bool SupportsMinimization, bool IsMinimal, int OriginalStateCount, int ReachableStateCount, int MinimizedStateCount);

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

    public string SymbolDisplay { get; set; } = string.Empty;

    // PDA-only
    public string? StackPopDisplay { get; set; }
    public string? StackPush { get; set; }
    public bool IsPDA { get; set; }
}
