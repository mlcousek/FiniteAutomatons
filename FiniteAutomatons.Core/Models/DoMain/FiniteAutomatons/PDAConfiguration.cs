using System.Collections.Immutable;

namespace FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;

public sealed record PDAConfiguration(int StateId, ImmutableStack<char> Stack)
{
    public string Key => $"{StateId}|{string.Concat(Stack)}";
}
