namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

public class DFA : Automaton
{
    public override void StepForward(AutomatonExecutionState state)
    {
        if (state.IsFinished || state.CurrentStateId == null)
        {
            state.IsAccepted = States.Any(s => s.Id == state.CurrentStateId && s.IsAccepting);
            return;
        }

        char symbol = state.Input[state.Position];
        var transition = Transitions
            .FirstOrDefault(t => t.FromStateId == state.CurrentStateId && t.Symbol == symbol);

        if (transition == null)
        {
            state.IsAccepted = false;
            state.Position = state.Input.Length; // Mark as finished
            return;
        }

        state.CurrentStateId = transition.ToStateId;
        state.Position++;

        if (state.Position >= state.Input.Length)
        {
            state.IsAccepted = States.Any(s => s.Id == state.CurrentStateId && s.IsAccepting);
        }
    }

    public override void ExecuteAll(AutomatonExecutionState state)
    {
        if (state.Input.Length == 0)
        {
            state.IsAccepted = States.Any(s => s.Id == state.CurrentStateId && s.IsAccepting);
            return;
        }

        while (!state.IsFinished && state.IsAccepted != false)
        {
            StepForward(state);
        }
    }
}
