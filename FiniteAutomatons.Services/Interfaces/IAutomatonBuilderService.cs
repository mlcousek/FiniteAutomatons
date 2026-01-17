using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

public interface IAutomatonBuilderService
{
    Automaton CreateAutomatonFromModel(AutomatonViewModel model);
    DFA CreateDFA(AutomatonViewModel model);
    NFA CreateNFA(AutomatonViewModel model);
    EpsilonNFA CreateEpsilonNFA(AutomatonViewModel model);
    PDA CreatePDA(AutomatonViewModel model);
}
