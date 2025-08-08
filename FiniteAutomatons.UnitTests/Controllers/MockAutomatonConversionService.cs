using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;

namespace FiniteAutomatons.UnitTests.Controllers;

public class MockAutomatonConversionService : IAutomatonConversionService
{
    public (AutomatonViewModel ConvertedModel, List<string> Warnings) ConvertAutomatonType(AutomatonViewModel model, AutomatonType newType)
    {
        var convertedModel = new AutomatonViewModel
        {
            Type = newType,
            States = [.. model.States ?? []],
            Transitions = [.. model.Transitions ?? []],
            Alphabet = [.. model.Alphabet ?? []],
            IsCustomAutomaton = model.IsCustomAutomaton
        };

        return (convertedModel, []);
    }

    public AutomatonViewModel ConvertToDFA(AutomatonViewModel model)
    {
        if (model.Type == AutomatonType.DFA)
        {
            return model;
        }

        // Mock conversion - just change the type
        return new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [.. model.States ?? []],
            Transitions = [.. model.Transitions ?? []],
            Alphabet = [.. model.Alphabet ?? []],
            IsCustomAutomaton = true
        };
    }
}