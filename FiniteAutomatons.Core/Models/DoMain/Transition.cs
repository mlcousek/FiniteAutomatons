namespace FiniteAutomatons.Core.Models.DoMain;

public class Transition
{
    public int FromStateId { get; set; }
    public int ToStateId { get; set; }
    public char Symbol { get; set; }
    public char? StackPop { get; set; } // null means no stack condition, '\0' means epsilon (no pop)
    public string? StackPush { get; set; } // null or empty => push nothing
}
