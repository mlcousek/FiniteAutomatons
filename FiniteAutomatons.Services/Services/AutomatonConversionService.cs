using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FiniteAutomatons.Services.Services;

public class AutomatonConversionService(IAutomatonBuilderService builderService, ILogger<AutomatonConversionService> logger) : IAutomatonConversionService
{
    private readonly IAutomatonBuilderService builderService = builderService;
    private readonly ILogger<AutomatonConversionService> logger = logger;

    public (AutomatonViewModel ConvertedModel, List<string> Warnings) ConvertAutomatonType(AutomatonViewModel model, AutomatonType newType)
    {
        var warnings = new List<string>();
        
        var convertedModel = new AutomatonViewModel
        {
            Type = newType,
            States = [.. model.States ?? []],
            Transitions = [.. model.Transitions ?? []],
            IsCustomAutomaton = model.IsCustomAutomaton
        };

        switch ((model.Type, newType))
        {
            case (AutomatonType.EpsilonNFA, AutomatonType.NFA):
                convertedModel.Transitions.RemoveAll(t => t.Symbol == '\0');
                warnings.Add("Epsilon transitions have been removed during conversion to NFA.");
                break;

            case (AutomatonType.EpsilonNFA, AutomatonType.DFA):
            case (AutomatonType.NFA, AutomatonType.DFA):
                warnings.Add($"Converting from {model.Type} to {newType} may require manual adjustment of transitions to ensure determinism.");
                break;

            case (AutomatonType.DFA, AutomatonType.NFA):
            case (AutomatonType.DFA, AutomatonType.EpsilonNFA):
            case (AutomatonType.NFA, AutomatonType.EpsilonNFA):
                // These conversions are generally safe (adding more flexibility)
                break;
        }

        logger.LogInformation("Converted automaton from {SourceType} to {TargetType} with {WarningCount} warnings", 
            model.Type, newType, warnings.Count);

        return (convertedModel, warnings);
    }

    /// <summary>
    /// Converts any automaton type to DFA
    /// </summary>
    /// <param name="model">The source automaton model</param>
    /// <returns>The converted DFA model</returns>
    public AutomatonViewModel ConvertToDFA(AutomatonViewModel model)
    {
        // Ensure collections are initialized
        model.States ??= [];
        model.Transitions ??= [];

        if (model.Type == AutomatonType.DFA)
        {
            logger.LogInformation("Automaton is already a DFA, returning original model");
            return model;
        }

        var automaton = builderService.CreateAutomatonFromModel(model);
        DFA convertedDFA;

        if (automaton is NFA nfa)
        {
            logger.LogInformation("Converting NFA to DFA");
            convertedDFA = nfa.ToDFA();
        }
        else if (automaton is EpsilonNFA enfa)
        {
            logger.LogInformation("Converting EpsilonNFA to DFA via NFA");
            // Convert EpsilonNFA -> NFA -> DFA
            var intermediateNFA = enfa.ToNFA();
            convertedDFA = intermediateNFA.ToDFA();
        }
        else
        {
            throw new InvalidOperationException("Cannot convert this automaton type to DFA");
        }

        // Create new model with converted DFA
        var convertedModel = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [.. convertedDFA.States],
            Transitions = [.. convertedDFA.Transitions],
            Input = model.Input ?? "",
            IsCustomAutomaton = true
        };

        logger.LogInformation("Successfully converted {SourceType} to DFA with {StateCount} states", 
            model.Type, convertedModel.States.Count);

        return convertedModel;
    }
}