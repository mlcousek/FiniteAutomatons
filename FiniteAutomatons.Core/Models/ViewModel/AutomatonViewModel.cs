namespace FiniteAutomatons.Core.Models.ViewModel;

using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
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
    public int? CurrentStateId { get; set; }
    public HashSet<int>? CurrentStates { get; set; }
    public int Position { get; set; }
    public bool? IsAccepted { get; set; }
    public string StateHistorySerialized { get; set; } = string.Empty;
    public string? StackSerialized { get; set; }
    public string? InitialStackSerialized { get; set; }
    public PDAAcceptanceMode AcceptanceMode { get; set; } = PDAAcceptanceMode.FinalStateAndEmptyStack;
    public bool IsCustomAutomaton { get; set; } = false;
    public string? SourceRegex { get; set; }
    public Dictionary<int, int>? StateMapping { get; set; }
    public Dictionary<int, List<int>>? MergedStateGroups { get; set; }
    public string? MinimizationReport { get; set; }
    public string? NewTransitionStackPop { get; set; }
    public string? NewTransitionStackPush { get; set; }

    public string CurrentStatesDisplay => CurrentStates != null && CurrentStates.Count != 0
        ? string.Join(", ", CurrentStates.OrderBy(x => x))
        : CurrentStateId?.ToString() ?? "";
    public bool SupportsEpsilonTransitions => Type == AutomatonType.EpsilonNFA || Type == AutomatonType.PDA;
    public string TypeDisplayName => Type switch
    {
        AutomatonType.DFA => "Deterministic Finite Automaton (DFA)",
        AutomatonType.NFA => "Nondeterministic Finite Automaton (NFA)",
        AutomatonType.EpsilonNFA => "Epsilon Nondeterministic Finite Automaton (ε-NFA)",
        AutomatonType.PDA => "Pushdown Automaton (PDA)",
        _ => Type.ToString()
    };
}
