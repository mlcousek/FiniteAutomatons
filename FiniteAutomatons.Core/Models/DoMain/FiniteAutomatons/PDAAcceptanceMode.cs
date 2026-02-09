namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

public enum PDAAcceptanceMode
{
    /// <summary>
    /// Accept if the automaton is in an accepting state AND the stack contains only the bottom marker.
    /// This is the default mode and combines both acceptance criteria.
    /// </summary>
    FinalStateAndEmptyStack = 0,

    /// <summary>
    /// Accept if the automaton is in an accepting state, regardless of stack contents.
    /// </summary>
    FinalStateOnly = 1,

    /// <summary>
    /// Accept if the stack is empty (contains only bottom marker), regardless of current state.
    /// </summary>
    EmptyStackOnly = 2
}
