using System.Collections.Immutable;

namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

public sealed class NPDAExecutionState : AutomatonExecutionState
{
    public HashSet<PDAConfiguration> Configurations { get; set; } = [];

    public Stack<HashSet<PDAConfiguration>> History { get; } = new();

    public NPDAExecutionState(string input, int startStateId, char bottomOfStack)
        : base(input, null)
    {
        var initialStack = ImmutableStack<char>.Empty.Push(bottomOfStack);
        Configurations = [new PDAConfiguration(startStateId, initialStack)];
    }

    public bool IsDead => Configurations.Count == 0;
}
