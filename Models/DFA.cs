namespace FiniteAutomatons.Models
{
    public class DFA : FiniteAutomaton
    {
        public bool IsDeterministic()
        {
            return Transitions.GroupBy(t => new { t.FromStateId, t.Symbol }).All(g => g.Count() == 1);
        }
    }
}
