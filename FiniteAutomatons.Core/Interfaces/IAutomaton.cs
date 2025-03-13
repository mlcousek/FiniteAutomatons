using FiniteAutomatons.Core.Models.DoMain;

namespace FiniteAutomatons.Core.Interfaces;

public interface IAutomaton
{
    bool Execute(string input);
    List<State> States { get; }
    List<Transition> Transitions { get; }
    int? StartStateId { get; }
}
