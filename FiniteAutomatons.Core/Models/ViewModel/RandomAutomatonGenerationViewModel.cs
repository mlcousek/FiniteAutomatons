using FiniteAutomatons.Core.Models.DoMain;
using System.ComponentModel.DataAnnotations;

namespace FiniteAutomatons.Core.Models.ViewModel;

public class RandomAutomatonGenerationViewModel
{
    [Display(Name = "Automaton Type")]
    public AutomatonType Type { get; set; } = AutomatonType.DFA;

    [Display(Name = "Number of States")]
    [Range(1, 50, ErrorMessage = "State count must be between 1 and 50")]
    public int StateCount { get; set; } = 5;

    [Display(Name = "Number of Transitions")]
    [Range(0, 500, ErrorMessage = "Transition count must be between 0 and 500")]
    public int TransitionCount { get; set; } = 8;

    [Display(Name = "Alphabet Size")]
    [Range(1, 10, ErrorMessage = "Alphabet size must be between 1 and 10")]
    public int AlphabetSize { get; set; } = 3;

    [Display(Name = "Accepting State Ratio")]
    [Range(0.0, 1.0, ErrorMessage = "Accepting state ratio must be between 0.0 and 1.0")]
    public double AcceptingStateRatio { get; set; } = 0.3;

    [Display(Name = "Random Seed (optional)")]
    public int? Seed { get; set; }

    // Helper properties for display
    public string TypeDisplayName => Type switch
    {
        AutomatonType.DFA => "Deterministic Finite Automaton (DFA)",
        AutomatonType.NFA => "Nondeterministic Finite Automaton (NFA)",
        AutomatonType.EpsilonNFA => "Epsilon Nondeterministic Finite Automaton (?-NFA)",
        _ => Type.ToString()
    };

    public int MinTransitionCount => StateCount;
    public int MaxTransitionCount => Type == AutomatonType.DFA ? StateCount * AlphabetSize : StateCount * StateCount;

    public bool IsValid => StateCount >= 1 && 
                          TransitionCount >= 0 && 
                          AlphabetSize >= 1 &&
                          AcceptingStateRatio >= 0.0 && AcceptingStateRatio <= 1.0 &&
                          (Type != AutomatonType.DFA || TransitionCount <= StateCount * AlphabetSize);
}