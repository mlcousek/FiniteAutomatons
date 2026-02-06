namespace FiniteAutomatons.Core.Models.Database;

/// <summary>
/// Indicates which data was saved with the automaton.
/// </summary>
public enum AutomatonSaveMode
{
    /// <summary>
    /// Only structure (states and transitions) was saved.
    /// </summary>
    Structure = 0,

    /// <summary>
    /// Structure and input string were saved (no execution state).
    /// </summary>
    WithInput = 1,

    /// <summary>
    /// Full execution state saved (structure, input, position, current states, history, stack).
    /// </summary>
    WithState = 2
}
