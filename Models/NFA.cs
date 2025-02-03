namespace FiniteAutomatons.Models
{
    public class NFA : FiniteAutomaton
    {
        public bool HasEpsilonTransitions()
        {
            return Transitions.Any(t => t.Symbol == 'ε');
        }
    }
}
