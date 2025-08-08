using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FiniteAutomatons.Services.Services;

/// <summary>
/// Service for building automaton instances from view models
/// </summary>
public class AutomatonBuilderService : IAutomatonBuilderService
{
    private readonly ILogger<AutomatonBuilderService> _logger;

    public AutomatonBuilderService(ILogger<AutomatonBuilderService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Creates an automaton instance from a view model
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The created automaton instance</returns>
    public Automaton CreateAutomatonFromModel(AutomatonViewModel model)
    {
        // Ensure model has required collections initialized
        model.States ??= [];
        model.Transitions ??= [];
        model.Alphabet ??= [];

        _logger.LogInformation("Creating automaton of type {Type} with {StateCount} states and {TransitionCount} transitions", 
            model.Type, model.States.Count, model.Transitions.Count);

        return model.Type switch
        {
            AutomatonType.DFA => CreateDFA(model),
            AutomatonType.NFA => CreateNFA(model),
            AutomatonType.EpsilonNFA => CreateEpsilonNFA(model),
            _ => throw new ArgumentException($"Unsupported automaton type: {model.Type}")
        };
    }

    /// <summary>
    /// Creates a DFA instance from a view model
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The created DFA instance</returns>
    public DFA CreateDFA(AutomatonViewModel model)
    {
        var dfa = new DFA();

        // Add states safely
        foreach (var state in model.States ?? [])
        {
            dfa.States.Add(state);
        }

        // Add transitions safely
        foreach (var transition in model.Transitions ?? [])
        {
            dfa.Transitions.Add(transition);
        }

        var startState = model.States?.FirstOrDefault(s => s.IsStart);
        if (startState != null)
        {
            dfa.SetStartState(startState.Id);
        }

        _logger.LogInformation("Created DFA with {StateCount} states and {TransitionCount} transitions", 
            dfa.States.Count, dfa.Transitions.Count);
        
        return dfa;
    }

    /// <summary>
    /// Creates an NFA instance from a view model
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The created NFA instance</returns>
    public NFA CreateNFA(AutomatonViewModel model)
    {
        var nfa = new NFA();

        // Add states safely
        foreach (var state in model.States ?? [])
        {
            nfa.States.Add(state);
        }

        // Add transitions safely
        foreach (var transition in model.Transitions ?? [])
        {
            nfa.Transitions.Add(transition);
        }

        var startState = model.States?.FirstOrDefault(s => s.IsStart);
        if (startState != null)
        {
            nfa.SetStartState(startState.Id);
        }

        _logger.LogInformation("Created NFA with {StateCount} states and {TransitionCount} transitions", 
            nfa.States.Count, nfa.Transitions.Count);
        
        return nfa;
    }

    /// <summary>
    /// Creates an Epsilon NFA instance from a view model
    /// </summary>
    /// <param name="model">The automaton view model</param>
    /// <returns>The created Epsilon NFA instance</returns>
    public EpsilonNFA CreateEpsilonNFA(AutomatonViewModel model)
    {
        var enfa = new EpsilonNFA();

        // Add states safely
        foreach (var state in model.States ?? [])
        {
            enfa.States.Add(state);
        }

        // Add transitions safely
        foreach (var transition in model.Transitions ?? [])
        {
            enfa.Transitions.Add(transition);
        }

        var startState = model.States?.FirstOrDefault(s => s.IsStart);
        if (startState != null)
        {
            enfa.SetStartState(startState.Id);
        }

        _logger.LogInformation("Created EpsilonNFA with {StateCount} states and {TransitionCount} transitions", 
            enfa.States.Count, enfa.Transitions.Count);
        
        return enfa;
    }
}