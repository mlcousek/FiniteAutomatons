﻿using FiniteAutomatons.Core.Interfaces;

namespace FiniteAutomatons.Core.Models.DoMain;

public abstract class Automaton : IAutomaton
{
    public List<State> States { get; } = [];
    public List<Transition> Transitions { get; } = [];
    public int? StartStateId => States.FirstOrDefault(s => s.IsStart)?.Id;

    protected int ValidateStartState()
    {
        if (StartStateId == null)
            throw new InvalidOperationException("No start state defined.");

        if (States.Count(s => s.IsStart) > 1)
            throw new InvalidOperationException("Multiple start states defined. Automaton must have exactly one start state.");

        return StartStateId.Value;
    }

    // Add a method to safely add states that ensures the single start state constraint
    public void AddState(State state)
    {
        ArgumentNullException.ThrowIfNull(state);

        if (state.IsStart && States.Any(s => s.IsStart))
        {
            throw new InvalidOperationException("Cannot add another start state. Automaton must have exactly one start state.");
        }

        States.Add(state);
    }

    // Method to safely change which state is the start state
    public void SetStartState(int stateId)
    {
        var state = States.FirstOrDefault(s => s.Id == stateId)
            ?? throw new ArgumentException($"State with ID {stateId} not found.");

        foreach (var s in States)
        {
            s.IsStart = false;
        }

        state.IsStart = true;
    }

    // Transition management methods
    public void AddTransition(Transition transition)
    {
        ArgumentNullException.ThrowIfNull(transition);
        ValidateTransition(transition);
        Transitions.Add(transition);
    }

    public void AddTransition(int fromStateId, int toStateId, char symbol)
    {
        var transition = new Transition
        {
            FromStateId = fromStateId,
            ToStateId = toStateId,
            Symbol = symbol
        };

        AddTransition(transition);
    }

    private void ValidateTransition(Transition transition)
    {
        if (!States.Any(s => s.Id == transition.FromStateId))
            throw new ArgumentException($"State with ID {transition.FromStateId} not found.");

        if (!States.Any(s => s.Id == transition.ToStateId))
            throw new ArgumentException($"State with ID {transition.ToStateId} not found.");
    }

    public List<Transition> FindTransitionsFromState(int stateId)
    {
        return [.. Transitions.Where(t => t.FromStateId == stateId)];
    }

    public List<Transition> FindTransitionsForSymbol(char symbol)
    {
        return [.. Transitions.Where(t => t.Symbol == symbol)];
    }

    public void RemoveTransition(Transition transition)
    {
        Transitions.Remove(transition);
    }

    public abstract bool Execute(string input);
}

