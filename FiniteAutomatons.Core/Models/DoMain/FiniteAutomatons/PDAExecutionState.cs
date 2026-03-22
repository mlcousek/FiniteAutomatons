namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

public sealed class PDAExecutionState(string input, int? stateId)
    : AutomatonExecutionState(input, stateId)
{
    public Stack<char> Stack { get; set; } = new();

    public Stack<Snapshot> History { get; } = new();

    public sealed class Snapshot
    {
        public int? StateId { get; set; }
        public int Position { get; set; }

        public char[] Stack { get; set; } = [];
    }
}
