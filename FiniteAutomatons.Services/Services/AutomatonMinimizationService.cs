using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FiniteAutomatons.Services.Services;

public class AutomatonMinimizationService(IAutomatonBuilderService builderService, ILogger<AutomatonMinimizationService> logger) : IAutomatonMinimizationService
{
    private readonly IAutomatonBuilderService builderService = builderService;
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
        // If minimalized DFA has single accepting start state and input empty we keep original acceptance potential (tests expect Already Minimal wording)
        var msg = minimizedModel.States.Count == model.States.Count ? "DFA minimized: already minimal (" + model.States.Count + " states)." : $"DFA minimized: {model.States.Count} -> {minimizedModel.States.Count} states.";
        logger.LogInformation(msg);
        return (minimizedModel, msg);
    }

    public MinimizationAnalysis AnalyzeAutomaton(AutomatonViewModel model)
    {
        model.States ??= [];
        model.Transitions ??= [];
        if (model.Type != AutomatonType.DFA || model.States.Count == 0)
        {
            return new MinimizationAnalysis(false, false, model.States.Count, 0, model.States.Count);
        }

        var dfa = builderService.CreateDFA(model);
        // Compute reachable states
        var start = model.States.FirstOrDefault(s => s.IsStart)?.Id;
        if (start == null)
        {
            // Without a start state we cannot minimize; treat as unsupported
            return new MinimizationAnalysis(true, false, model.States.Count, 0, model.States.Count);
        }

        var reachable = new HashSet<int>();
        var q = new Queue<int>();
        q.Enqueue(start.Value);
        reachable.Add(start.Value);
        while (q.Count > 0)
        {
            var cur = q.Dequeue();
            foreach (var t in model.Transitions.Where(t => t.FromStateId == cur))
            {
                if (reachable.Add(t.ToStateId)) q.Enqueue(t.ToStateId);
            }
        }
        int reachableCount = reachable.Count;

        // Run full minimization to get minimized count (re-use existing method)
        var minimized = dfa.MinimalizeDFA();
        int minimizedCount = minimized.States.Count;

        bool isMinimal = minimizedCount == reachableCount && reachableCount == model.States.Count;
        return new MinimizationAnalysis(true, isMinimal, model.States.Count, reachableCount, minimizedCount);
    }
}
