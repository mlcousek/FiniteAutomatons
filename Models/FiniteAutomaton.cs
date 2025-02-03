namespace FiniteAutomatons.Models
{
    public class FiniteAutomaton
    {
        public List<State> States { get; set; } = new List<State>();
        public List<Transition> Transitions { get; set; } = new List<Transition>();
    }
}
