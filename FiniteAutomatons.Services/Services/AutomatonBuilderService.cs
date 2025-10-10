using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FiniteAutomatons.Services.Services;

public class AutomatonBuilderService(ILogger<AutomatonBuilderService> logger) : IAutomatonBuilderService
{
    private readonly ILogger<AutomatonBuilderService> logger = logger;

    public Automaton CreateAutomatonFromModel(AutomatonViewModel model)
    {
        model.States ??= [];
        model.Transitions ??= [];

        if (model.States.Count(s => s.IsStart) > 1)
        {
            throw new InvalidOperationException("Multiple start states defined. Automaton must have exactly one start state.");
        }

        logger.LogInformation("Creating automaton of type {Type} with {StateCount} states and {TransitionCount} transitions", 
            model.Type, model.States.Count, model.Transitions.Count);

        return model.Type switch
        {
            AutomatonType.DFA => CreateDFA(model),
            AutomatonType.NFA => CreateNFA(model),
            AutomatonType.EpsilonNFA => CreateEpsilonNFA(model),
            AutomatonType.PDA => CreatePDA(model),
            _ => throw new ArgumentException($"Unsupported automaton type: {model.Type}")
        };
    }

    public DFA CreateDFA(AutomatonViewModel model)
    {
        var dfa = new DFA();

        foreach (var state in model.States ?? [])
        {
            dfa.States.Add(state);
        }

        foreach (var transition in model.Transitions ?? [])
        {
            dfa.Transitions.Add(transition);
        }

        var startState = model.States?.FirstOrDefault(s => s.IsStart);
        if (startState != null)
        {
            dfa.SetStartState(startState.Id);
        }

        logger.LogInformation("Created DFA with {StateCount} states and {TransitionCount} transitions", 
            dfa.States.Count, dfa.Transitions.Count);
        
        return dfa;
    }

    public NFA CreateNFA(AutomatonViewModel model)
    {
        var nfa = new NFA();

        foreach (var state in model.States ?? [])
        {
            nfa.States.Add(state);
        }

        foreach (var transition in model.Transitions ?? [])
        {
            nfa.Transitions.Add(transition);
        }

        var startState = model.States?.FirstOrDefault(s => s.IsStart);
        if (startState != null)
        {
            nfa.SetStartState(startState.Id);
        }

        logger.LogInformation("Created NFA with {StateCount} states and {TransitionCount} transitions", 
            nfa.States.Count, nfa.Transitions.Count);
        
        return nfa;
    }

    public EpsilonNFA CreateEpsilonNFA(AutomatonViewModel model)
    {
        var enfa = new EpsilonNFA();

        foreach (var state in model.States ?? [])
        {
            enfa.States.Add(state);
        }

        foreach (var transition in model.Transitions ?? [])
        {
            enfa.Transitions.Add(transition);
        }

        var startState = model.States?.FirstOrDefault(s => s.IsStart);
        if (startState != null)
        {
            enfa.SetStartState(startState.Id);
        }

        logger.LogInformation("Created EpsilonNFA with {StateCount} states and {TransitionCount} transitions", 
            enfa.States.Count, enfa.Transitions.Count);
        
        return enfa;
    }

    public PDA CreatePDA(AutomatonViewModel model)
    {
        var pda = new PDA();

        foreach (var state in model.States ?? [])
        {
            pda.States.Add(state);
        }

        foreach (var transition in model.Transitions ?? [])
        {
            pda.Transitions.Add(transition);
        }

        var startState = model.States?.FirstOrDefault(s => s.IsStart);
        if (startState != null)
        {
            pda.SetStartState(startState.Id);
        }

        logger.LogInformation("Created PDA with {StateCount} states and {TransitionCount} transitions", 
            pda.States.Count, pda.Transitions.Count);
        
        return pda;
    }
}
