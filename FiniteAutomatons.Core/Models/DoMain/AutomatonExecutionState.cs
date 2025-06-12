namespace FiniteAutomatons.Core.Models.DoMain;

public class AutomatonExecutionState(string input, int? startStateId = null, HashSet<int>? startStates = null)
{
    public int? CurrentStateId { get; set; } = startStateId;
    public HashSet<int>? CurrentStates { get; set; } = startStates;
    public string Input { get; } = input;
    public int Position { get; set; } = 0;
    public bool IsFinished => Position >= Input.Length;
    public bool? IsAccepted { get; set; } = null;
}
