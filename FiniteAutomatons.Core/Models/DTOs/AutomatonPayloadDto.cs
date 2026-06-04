using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Core.Models.DTOs;

public sealed class AutomatonPayloadDto
{
    public AutomatonType Type { get; set; }
    public List<State>? States { get; set; }
    public List<Transition>? Transitions { get; set; }
    public PDAAcceptanceMode AcceptanceMode { get; set; } = PDAAcceptanceMode.FinalStateAndEmptyStack;
    public string? InitialStackSerialized { get; set; }
}
