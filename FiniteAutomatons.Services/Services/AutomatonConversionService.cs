using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;
using FiniteAutomatons.Core.Utilities; // added for NormalizeEpsilonTransitions

namespace FiniteAutomatons.Services.Services;

public class AutomatonConversionService(IAutomatonBuilderService builderService, ILogger<AutomatonConversionService> logger) : IAutomatonConversionService
{
    private readonly IAutomatonBuilderService builderService = builderService;
    private readonly ILogger<AutomatonConversionService> logger = logger;

    public (AutomatonViewModel ConvertedModel, List<string> Warnings) ConvertAutomatonType(AutomatonViewModel model, AutomatonType newType)
    {
        model.States ??= [];
        model.Transitions ??= [];
        var warnings = new List<string>();

        // Always normalize epsilon aliases first so internal representation uses '\0'
        if (model.Type == AutomatonType.EpsilonNFA)
        {
            model.NormalizeEpsilonTransitions();
        }

        // Full conversion: EpsilonNFA -> NFA using epsilon-closure elimination (removes epsilon transitions)
        if (model.Type == AutomatonType.EpsilonNFA && newType == AutomatonType.NFA)
        {
            try
            {
                var built = builderService.CreateAutomatonFromModel(model);
                if (built is EpsilonNFA enfa)
                {
                    var nfa = enfa.ToNFA();
                    var convertedModelFull = new AutomatonViewModel
                    {
                        Type = AutomatonType.NFA,
                        States = [.. nfa.States],
                        Transitions = [.. nfa.Transitions],
                        Input = model.Input ?? string.Empty,
                        IsCustomAutomaton = model.IsCustomAutomaton
                    };
                    warnings.Add("Converted EpsilonNFA to NFA via epsilon-closure elimination. Epsilon transitions removed.");
                    logger.LogInformation("Performed full EpsilonNFA -> NFA conversion. States={StateCount} Transitions={TransitionCount}", convertedModelFull.States.Count, convertedModelFull.Transitions.Count);
                    return (convertedModelFull, warnings);
                }
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Failed semantic conversion EpsilonNFA -> NFA; falling back to simple removal.");
            }
            // Fallback: just remove epsilon transitions (now guaranteed normalized to '\0')
            var fallback = new AutomatonViewModel
            {
                Type = AutomatonType.NFA,
                States = [.. model.States],
                Transitions = [.. model.Transitions.Where(t => t.Symbol != '\0')],
                Input = model.Input ?? string.Empty,
                IsCustomAutomaton = model.IsCustomAutomaton
            };
            warnings.Add("Performed fallback conversion by removing epsilon transitions.");
            return (fallback, warnings);
        }

        // Shallow conversions (label/type changes) for other paths
        var shallow = new AutomatonViewModel
        {
            Type = newType,
            States = [.. model.States],
            Transitions = [.. model.Transitions],
            Input = model.Input ?? string.Empty,
            IsCustomAutomaton = model.IsCustomAutomaton
        };

        switch ((model.Type, newType))
        {
            case (AutomatonType.EpsilonNFA, AutomatonType.DFA):
            case (AutomatonType.NFA, AutomatonType.DFA):
                warnings.Add("Shallow conversion only. Use 'Minimalize' / ConvertToDFA for full determinization.");
                break;
        }

        logger.LogInformation("Shallow converted automaton from {From} to {To}", model.Type, newType);
        return (shallow, warnings);
    }

    public AutomatonViewModel ConvertToDFA(AutomatonViewModel model)
    {
        model.States ??= [];
        model.Transitions ??= [];

        // Normalize epsilon before building (important if UI posted a visible alias like '?')
        if (model.Type == AutomatonType.EpsilonNFA)
        {
            model.NormalizeEpsilonTransitions();
        }

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
            var intermediateNFA = enfa.ToNFA();
            convertedDFA = intermediateNFA.ToDFA();
        }
        else
        {
            throw new InvalidOperationException("Cannot convert this automaton type to DFA");
        }

        var convertedModel = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [.. convertedDFA.States],
            Transitions = [.. convertedDFA.Transitions],
            Input = model.Input ?? string.Empty,
            IsCustomAutomaton = true
        };

        logger.LogInformation("Successfully converted {SourceType} to DFA with {StateCount} states", model.Type, convertedModel.States.Count);

        return convertedModel;
    }
}
