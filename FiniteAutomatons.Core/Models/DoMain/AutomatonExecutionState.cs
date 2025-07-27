namespace FiniteAutomatons.Core.Models.DoMain;

public class AutomatonExecutionState(string input, int? StateId = null, HashSet<int>? States = null)
{
    public int? CurrentStateId { get; set; } = StateId;
    public HashSet<int>? CurrentStates { get; set; } = States;
    public string Input { get; } = input;
    public int Position { get; set; } = 0;
    public bool IsFinished => Position >= Input.Length;
    public bool? IsAccepted { get; set; } = null;
    public Stack<HashSet<int>> StateHistory { get; } = new();
}
