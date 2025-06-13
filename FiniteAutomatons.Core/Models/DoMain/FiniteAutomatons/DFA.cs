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

        state.StateHistory.Push([state.CurrentStateId.Value]);

        char symbol = state.Input[state.Position];
        var transition = Transitions
            .FirstOrDefault(t => t.FromStateId == state.CurrentStateId && t.Symbol == symbol);

        if (transition == null)
        {
            state.IsAccepted = false;
            state.Position = state.Input.Length;
            return;
        }

        state.CurrentStateId = transition.ToStateId;
        state.Position++;

        if (state.Position >= state.Input.Length)
        {
            state.IsAccepted = States.Any(s => s.Id == state.CurrentStateId && s.IsAccepting);
        }
    }

    public override void StepBackward(AutomatonExecutionState state)
    {
        if (state.Position == 0)
            return;

        state.Position--;

        // Restore the previous state from the history stack
        if (state.StateHistory.Count > 0)
        {
            var prevStates = state.StateHistory.Pop();
            // DFA only ever has one state in the set
            state.CurrentStateId = prevStates.FirstOrDefault();
        }
        else
        {
            // Fallback: recalculate from start if history is missing
            state.CurrentStateId = States.First(s => s.IsStart).Id;
            for (int i = 0; i < state.Position; i++)
            {
                char symbol = state.Input[i];
                var transition = Transitions
                    .FirstOrDefault(t => t.FromStateId == state.CurrentStateId && t.Symbol == symbol);

                if (transition == null)
                {
                    state.CurrentStateId = null;
                    state.IsAccepted = false;
                    return;
                }

                state.CurrentStateId = transition.ToStateId;
            }
        }

        state.IsAccepted = null;
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
