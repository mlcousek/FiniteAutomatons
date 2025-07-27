namespace FiniteAutomatons.Core.Models.ViewModel;

using FiniteAutomatons.Core.Models.DoMain;
using System.Collections.Generic;

public class DfaViewModel
{
    public List<State> States { get; set; } = [];
    public List<Transition> Transitions { get; set; } = [];
    public List<char> Alphabet { get; set; } = [];
    public string Input { get; set; } = string.Empty;
    public bool? Result { get; set; }
}
