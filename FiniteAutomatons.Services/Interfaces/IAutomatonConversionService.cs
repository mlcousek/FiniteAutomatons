using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

/// <summary>
/// Service for converting between different automaton types
/// </summary>
public interface IAutomatonConversionService
{
    /// <summary>
    /// Converts an automaton from one type to another
    /// </summary>
    /// <param name="model">The source automaton model</param>
    /// <param name="newType">The target automaton type</param>
    /// <returns>A tuple containing the converted model and any warnings</returns>
    (AutomatonViewModel ConvertedModel, List<string> Warnings) ConvertAutomatonType(AutomatonViewModel model, AutomatonType newType);

    /// <summary>
    /// Converts any automaton type to DFA
    /// </summary>
    /// <param name="model">The source automaton model</param>
    /// <returns>The converted DFA model</returns>
    AutomatonViewModel ConvertToDFA(AutomatonViewModel model);
}