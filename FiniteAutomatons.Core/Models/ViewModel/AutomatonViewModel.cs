namespace FiniteAutomatons.Core.Models.ViewModel;

using FiniteAutomatons.Core.Models.DoMain;
using System.Collections.Generic;
using System.Linq;

public enum AutomatonType
{
    DFA,
    NFA,
    EpsilonNFA,
    PDA // Pushdown Automaton (deterministic for initial implementation)
}

public class AutomatonViewModel
{
    public AutomatonType Type { get; set; } = AutomatonType.DFA;
    public List<State> States { get; set; } = [];
    public List<Transition> Transitions { get; set; } = [];

    public IReadOnlyList<char> Alphabet => [.. Transitions
        .Where(t => t.Symbol != '\0')
        .Select(t => t.Symbol)
        .Distinct()
        .OrderBy(c => c)];

    public string Input { get; set; } = string.Empty;
    public bool? Result { get; set; }
    public bool HasExecuted { get; set; } = false;
    public int? CurrentStateId { get; set; } // For DFA / PDA - single current state
    public HashSet<int>? CurrentStates { get; set; } // For NFA/EpsilonNFA - current set of states
    public int Position { get; set; }
    public bool? IsAccepted { get; set; }
    public string StateHistorySerialized { get; set; } = string.Empty; // for round-tripping state history
    public string? StackSerialized { get; set; } // PDA stack serialization (top-first JSON array of chars)
    public bool IsCustomAutomaton { get; set; } = false;
    public string CurrentStatesDisplay => CurrentStates != null && CurrentStates.Count != 0
        ? string.Join(", ", CurrentStates.OrderBy(x => x))
        : CurrentStateId?.ToString() ?? "";
    public bool SupportsEpsilonTransitions => Type == AutomatonType.EpsilonNFA || Type == AutomatonType.PDA; // PDA supports epsilon input moves
    public string TypeDisplayName => Type switch
    {
        AutomatonType.DFA => "Deterministic Finite Automaton (DFA)",
        AutomatonType.NFA => "Nondeterministic Finite Automaton (NFA)",
        AutomatonType.EpsilonNFA => "Epsilon Nondeterministic Finite Automaton (ε-NFA)",
        AutomatonType.PDA => "Pushdown Automaton (PDA)",
        _ => Type.ToString()
    };
}
