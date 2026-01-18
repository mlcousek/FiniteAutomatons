using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FiniteAutomatons.Services.Services;

public class AutomatonMinimizationService(IAutomatonBuilderService builderService, IAutomatonAnalysisService analysisService, ILogger<AutomatonMinimizationService> logger) : IAutomatonMinimizationService
{
    private readonly IAutomatonBuilderService builderService = builderService;
    private readonly IAutomatonAnalysisService analysisService = analysisService;
    private readonly ILogger<AutomatonMinimizationService> logger = logger;

    public (AutomatonViewModel Result, string Message) MinimizeDfa(AutomatonViewModel model)
    {
        model.States ??= [];
        model.Transitions ??= [];
        if (model.Type != AutomatonType.DFA)
        {
            return (model, "Minimization supported only for DFA.");
        }
        var automaton = builderService.CreateDFA(model);
        var minimized = automaton.MinimalizeDFA();
        var minimizedModel = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [.. minimized.States],
            Transitions = [.. minimized.Transitions],
            Input = model.Input ?? string.Empty,
            IsCustomAutomaton = true,
            // Manually clear execution state (keep input)
            Result = null,
            CurrentStateId = null,
            CurrentStates = null,
            Position = 0,
            IsAccepted = null,
            StateHistorySerialized = string.Empty,
            HasExecuted = false
        };
        // copy mapping metadata if available
        if (minimized.StateMapping != null)
        {
            minimizedModel.StateMapping = new Dictionary<int, int>(minimized.StateMapping);
            minimizedModel.MergedStateGroups = minimized.MergedStateGroups?.ToDictionary(k => k.Key, v => v.Value.OrderBy(x => x).ToList());
            minimizedModel.MinimizationReport = minimized.GetMinimizationReport();
        }
        // If minimalized DFA has single accepting start state and input empty we keep original acceptance potential (tests expect Already Minimal wording)
        var msg = minimizedModel.States.Count == model.States.Count ? "DFA minimized: already minimal (" + model.States.Count + " states)." : $"DFA minimized: {model.States.Count} -> {minimizedModel.States.Count} states.";
        logger.LogInformation(msg);
        return (minimizedModel, msg);
    }

    public MinimizationAnalysis AnalyzeAutomaton(AutomatonViewModel model)
    {
        EnsureCollections(model);

        if (model.Type != AutomatonType.DFA || model.States.Count == 0)
        {
            return new MinimizationAnalysis(false, false, model.States.Count, 0, model.States.Count);
        }

        var start = GetStartStateIdOrNull(model);
        if (start == null)
        {
            return new MinimizationAnalysis(true, false, model.States.Count, 0, model.States.Count);
        }
        int reachableCount = analysisService.GetReachableCount(model.Transitions, start.Value);
        int minimizedCount = ComputeMinimizedStatesCount(model);

        bool isMinimal = minimizedCount == reachableCount && reachableCount == model.States.Count;
        return new MinimizationAnalysis(true, isMinimal, model.States.Count, reachableCount, minimizedCount);
    }

    private static void EnsureCollections(AutomatonViewModel model)
    {
        model.States ??= [];
        model.Transitions ??= [];
    }

    private static int? GetStartStateIdOrNull(AutomatonViewModel model)
    {
        return model.States.FirstOrDefault(s => s.IsStart)?.Id;
    }

    private int ComputeMinimizedStatesCount(AutomatonViewModel model)
    {
        var dfa = builderService.CreateDFA(model);
        var minimized = dfa.MinimalizeDFA();
        return minimized.States.Count;
    }
}
