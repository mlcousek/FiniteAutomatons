using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FiniteAutomatons.IntegrationTests;

public class AlphabetAfterConversionTests
{
    [Fact]
    public void ConvertEpsilonNfaToNfa_AlphabetShouldNotContainNullCharacter()
    {
        var builder = new AutomatonBuilderService(NullLogger<AutomatonBuilderService>.Instance);
        var service = new AutomatonConversionService(builder, NullLogger<AutomatonConversionService>.Instance);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true },
                new() { Id = 3, IsStart = false, IsAccepting = false }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0' }, // Epsilon
                new() { FromStateId = 1, ToStateId = 3, Symbol = 'a' },
                new() { FromStateId = 3, ToStateId = 2, Symbol = 'b' }
            ],
            Input = "",
            IsCustomAutomaton = true
        };

        var (converted, warnings) = service.ConvertAutomatonType(model, AutomatonType.NFA);

        foreach (var transition in converted.Transitions)
        {
            transition.Symbol.ShouldNotBe('\0', $"Transition from {transition.FromStateId} to {transition.ToStateId} should not have null symbol");
        }

        converted.Alphabet.ShouldNotContain('\0', "Alphabet should not contain null character after conversion");

        converted.Alphabet.Count.ShouldBe(2);
        converted.Alphabet.ShouldContain('a');
        converted.Alphabet.ShouldContain('b');

        Console.WriteLine($"Converted transitions: {string.Join(", ", converted.Transitions.Select(t => $"{t.FromStateId}-{t.Symbol}->{t.ToStateId}"))}");
        Console.WriteLine($"Alphabet: {string.Join(", ", converted.Alphabet)}");
    }
}
