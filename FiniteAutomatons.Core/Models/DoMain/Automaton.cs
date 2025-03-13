using FiniteAutomatons.Core.Interfaces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FiniteAutomatons.Core.Models.DoMain;

public abstract class Automaton : IAutomaton
{
    public List<State> States { get; } = new();
    public List<Transition> Transitions { get; } = new();
    public int? StartStateId => States.FirstOrDefault(s => s.IsStart)?.Id;

    protected void ValidateStartState()
    {
        if (StartStateId == null)
            throw new InvalidOperationException("No start state defined.");
    }

    public abstract bool Execute(string input);
}

