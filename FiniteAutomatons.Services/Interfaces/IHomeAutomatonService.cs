using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

public interface IHomeAutomatonService
{
    AutomatonViewModel GenerateDefaultAutomaton();
    AutomatonViewModel CreateFallbackAutomaton();
}
