namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

public class DFA : Automaton
{
    public override bool Execute(string input)
    {
        var currentStateId = ValidateStartState();

        foreach (var symbol in input)
        {
            var transition = Transitions
                .FirstOrDefault(t => t.FromStateId == currentStateId && t.Symbol == symbol);
            if (transition == null)
            {
                return false;
            }

            currentStateId = transition.ToStateId;
        }

        return States.Any(s => s.Id == currentStateId && s.IsAccepting);
    }
}
