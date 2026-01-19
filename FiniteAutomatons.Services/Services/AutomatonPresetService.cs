using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FiniteAutomatons.Services.Services;

public class AutomatonPresetService(
    IAutomatonGeneratorService generatorService,
    IAutomatonMinimizationService minimizationService,
    ILogger<AutomatonPresetService> logger) : IAutomatonPresetService
{
    private readonly IAutomatonGeneratorService generatorService = generatorService;
    private readonly IAutomatonMinimizationService minimizationService = minimizationService;
    private readonly ILogger<AutomatonPresetService> logger = logger;

    public AutomatonViewModel GenerateMinimalizedDfa(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null)
    {
        logger.LogInformation("Generating minimalized DFA preset with {StateCount} states", stateCount);

        var dfa = generatorService.GenerateRandomAutomaton(AutomatonType.DFA, stateCount, transitionCount, alphabetSize, acceptingRatio, seed);
        var (minModel, message) = minimizationService.MinimizeDfa(dfa);

        if (minModel == null)
        {
            logger.LogWarning("Failed to minimize DFA: {Message}", message);
            throw new InvalidOperationException($"Failed to minimize generated DFA: {message}");
        }

        minModel.MinimizationReport = null;
        minModel.StateMapping = null;
        minModel.MergedStateGroups = null;

        logger.LogInformation("Successfully generated minimalized DFA with {OriginalStates} -> {MinimizedStates} states",
            dfa.States.Count, minModel.States.Count);

        return minModel;
    }

    public AutomatonViewModel GenerateUnminimalizedDfa(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null)
    {
        logger.LogInformation("Generating unminimalized DFA preset with {StateCount} states", stateCount);

        // Generate a base DFA
        var baseDfa = generatorService.GenerateRandomAutomaton(AutomatonType.DFA, stateCount, transitionCount, alphabetSize, acceptingRatio, seed);

        // Check if it's already unminimalized by attempting minimization
        var (minimized, _) = minimizationService.MinimizeDfa(baseDfa);

        if (minimized != null && minimized.States.Count < baseDfa.States.Count)
        {
            // Already unminimalized - it has redundant states
            logger.LogInformation("Generated DFA is already unminimalized ({OriginalStates} -> {MinimalStates} states)",
                baseDfa.States.Count, minimized.States.Count);
            return baseDfa;
        }

        // DFA is minimal - we need to add equivalent states to make it unminimalized
        logger.LogInformation("Generated DFA is minimal, adding equivalent states to create unminimalized version");
        return AddEquivalentStates(baseDfa, seed);
    }

    private AutomatonViewModel AddEquivalentStates(AutomatonViewModel dfa, int? seed)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();

        // Create a copy of the DFA
        var unminimalized = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [.. dfa.States],
            Transitions = [.. dfa.Transitions],
            Input = dfa.Input,
            IsCustomAutomaton = dfa.IsCustomAutomaton
        };

        // Find non-start, non-accepting states to duplicate (safer to duplicate)
        var candidateStates = unminimalized.States
            .Where(s => !s.IsStart && !s.IsAccepting)
            .ToList();

        // If no candidates, use any non-start state
        if (candidateStates.Count == 0)
        {
            candidateStates = [.. unminimalized.States.Where(s => !s.IsStart)];
        }

        if (candidateStates.Count == 0)
        {
            logger.LogWarning("Cannot add equivalent states - DFA has only start state");
            return unminimalized;
        }

        // Duplicate 1-2 states to create equivalent states
        var statesToDuplicate = Math.Min(2, candidateStates.Count);
        var nextStateId = unminimalized.States.Max(s => s.Id) + 1;

        for (int i = 0; i < statesToDuplicate; i++)
        {
            var originalState = candidateStates[random.Next(candidateStates.Count)];

            // Create an equivalent state (same accepting status)
            var equivalentState = new State
            {
                Id = nextStateId++,
                IsStart = false, // Never make duplicate a start state
                IsAccepting = originalState.IsAccepting
            };

            unminimalized.States.Add(equivalentState);

            // Copy all transitions FROM the original state to the equivalent state
            var outgoingTransitions = unminimalized.Transitions
                .Where(t => t.FromStateId == originalState.Id)
                .ToList();

            foreach (var transition in outgoingTransitions)
            {
                unminimalized.Transitions.Add(new Transition
                {
                    FromStateId = equivalentState.Id,
                    ToStateId = transition.ToStateId,
                    Symbol = transition.Symbol
                });
            }

            logger.LogInformation("Added equivalent state q{EquivalentId} for q{OriginalId}",
                equivalentState.Id, originalState.Id);
        }

        logger.LogInformation("Successfully created unminimalized DFA with {OriginalStates} + {AddedStates} states",
            dfa.States.Count, statesToDuplicate);

        return unminimalized;
    }

    public AutomatonViewModel GenerateNondeterministicNfa(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null)
    {
        logger.LogInformation("Generating nondeterministic NFA preset with {StateCount} states", stateCount);

        // Generate a base NFA
        var baseNfa = generatorService.GenerateRandomAutomaton(AutomatonType.NFA, stateCount, transitionCount, alphabetSize, acceptingRatio, seed);

        // Check if it's already nondeterministic (has multiple transitions from same state on same symbol)
        if (IsNondeterministic(baseNfa))
        {
            logger.LogInformation("Generated NFA is already nondeterministic");
            return baseNfa;
        }

        // NFA is deterministic - we need to add nondeterministic transitions
        logger.LogInformation("Generated NFA is deterministic, adding nondeterministic transitions");
        return AddNondeterministicTransitions(baseNfa, seed);
    }

    public AutomatonViewModel GenerateNondeterministicEpsilonNfa(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null)
    {
        logger.LogInformation("Generating nondeterministic epsilon-NFA preset with {StateCount} states", stateCount);

        // Generate a base NFA
        var baseeNfa = generatorService.GenerateRandomAutomaton(AutomatonType.EpsilonNFA, stateCount, transitionCount, alphabetSize, acceptingRatio, seed);

        // Check if it's already nondeterministic (has multiple transitions from same state on same symbol)
        if (IsNondeterministic(baseeNfa))
        {
            logger.LogInformation("Generated eNFA is already nondeterministic");
            return baseeNfa;
        }

        // eNFA is deterministic - we need to add nondeterministic transitions
        logger.LogInformation("Generated Epsilon-NFA is deterministic, adding nondeterministic transitions");
        return AddNondeterministicTransitions(baseeNfa, seed);
    }

    private bool IsNondeterministic(AutomatonViewModel nfa)
    {
        // Check if any state has multiple transitions on the same symbol
        var transitionGroups = nfa.Transitions
            .GroupBy(t => new { t.FromStateId, t.Symbol })
            .Where(g => g.Count() > 1);

        return transitionGroups.Any();
    }

    private AutomatonViewModel AddNondeterministicTransitions(AutomatonViewModel nfa, int? seed)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();

        // Create a copy of the NFA - preserve the original type (NFA or EpsilonNFA)
        var nondetNfa = new AutomatonViewModel
        {
            Type = nfa.Type,
            States = [.. nfa.States],
            Transitions = [.. nfa.Transitions],
            Input = nfa.Input,
            IsCustomAutomaton = nfa.IsCustomAutomaton
        };

        // Get the alphabet (non-epsilon symbols)
        var alphabet = nondetNfa.Transitions
            .Where(t => t.Symbol != '\0')
            .Select(t => t.Symbol)
            .Distinct()
            .ToList();

        if (alphabet.Count == 0)
        {
            logger.LogWarning("Cannot add nondeterministic transitions - no alphabet symbols");
            return nondetNfa;
        }

        // Find states that have outgoing transitions
        var statesWithTransitions = nondetNfa.Transitions
            .Select(t => t.FromStateId)
            .Distinct()
            .ToList();

        if (statesWithTransitions.Count == 0)
        {
            logger.LogWarning("Cannot add nondeterministic transitions - no states with transitions");
            return nondetNfa;
        }

        // Add 2-3 nondeterministic transitions to guarantee nondeterminism
        var transitionsToAdd = Math.Min(3, statesWithTransitions.Count);

        for (int i = 0; i < transitionsToAdd; i++)
        {
            var fromState = statesWithTransitions[random.Next(statesWithTransitions.Count)];
            var symbol = alphabet[random.Next(alphabet.Count)];

            // Find existing transitions from this state on this symbol
            var existingTargets = nondetNfa.Transitions
                .Where(t => t.FromStateId == fromState && t.Symbol == symbol)
                .Select(t => t.ToStateId)
                .ToHashSet();

            // Find a different target state to create nondeterminism
            var availableTargets = nondetNfa.States
                .Select(s => s.Id)
                .Where(id => !existingTargets.Contains(id))
                .ToList();

            if (availableTargets.Count > 0)
            {
                var toState = availableTargets[random.Next(availableTargets.Count)];

                nondetNfa.Transitions.Add(new Transition
                {
                    FromStateId = fromState,
                    ToStateId = toState,
                    Symbol = symbol
                });

                logger.LogInformation("Added nondeterministic transition: q{From} --{Symbol}--> q{To}",
                    fromState, symbol, toState);
            }
        }

        logger.LogInformation("Successfully created nondeterministic NFA with {OriginalTransitions} + {AddedTransitions} transitions",
            nfa.Transitions.Count, transitionsToAdd);

        return nondetNfa;
    }

    public AutomatonViewModel GenerateRandomDfa(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null)
    {
        logger.LogInformation("Generating random DFA preset with {StateCount} states", stateCount);

        return generatorService.GenerateRandomAutomaton(
            AutomatonType.DFA,
            stateCount,
            transitionCount,
            alphabetSize,
            acceptingRatio,
            seed);
    }

    public AutomatonViewModel GenerateRandomNfa(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null)
    {
        logger.LogInformation("Generating random NFA preset with {StateCount} states", stateCount);

        return generatorService.GenerateRandomAutomaton(
            AutomatonType.NFA,
            stateCount,
            transitionCount,
            alphabetSize,
            acceptingRatio,
            seed);
    }

    public AutomatonViewModel GenerateEpsilonNfa(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null)
    {
        logger.LogInformation("Generating ε-NFA preset with epsilon transitions, {StateCount} states", stateCount);

        var baseEnfa = generatorService.GenerateRandomAutomaton(
            AutomatonType.EpsilonNFA,
            stateCount,
            transitionCount,
            alphabetSize,
            acceptingRatio,
            seed);

        // Check if it already has epsilon transitions
        var hasEpsilonTransitions = baseEnfa.Transitions.Any(t => t.Symbol == '\0');

        if (hasEpsilonTransitions)
        {
            logger.LogInformation("Generated ε-NFA already has epsilon transitions");
            return baseEnfa;
        }

        // No epsilon transitions - add some
        logger.LogInformation("Generated ε-NFA has no epsilon transitions, adding them");
        return AddEpsilonTransitions(baseEnfa, seed);
    }

    private AutomatonViewModel AddEpsilonTransitions(AutomatonViewModel enfa, int? seed)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();

        // Create a copy
        var withEpsilon = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States = new List<State>(enfa.States),
            Transitions = new List<Transition>(enfa.Transitions),
            Input = enfa.Input,
            IsCustomAutomaton = enfa.IsCustomAutomaton
        };

        if (withEpsilon.States.Count < 2)
        {
            logger.LogWarning("Cannot add epsilon transitions - need at least 2 states");
            return withEpsilon;
        }

        // Add 2-3 epsilon transitions between random states
        var epsilonTransitionsToAdd = Math.Min(3, withEpsilon.States.Count);

        for (int i = 0; i < epsilonTransitionsToAdd; i++)
        {
            var fromState = withEpsilon.States[random.Next(withEpsilon.States.Count)];
            var toState = withEpsilon.States[random.Next(withEpsilon.States.Count)];

            // Avoid self-loops and duplicate epsilon transitions
            if (fromState.Id != toState.Id &&
                !withEpsilon.Transitions.Any(t => t.FromStateId == fromState.Id && t.ToStateId == toState.Id && t.Symbol == '\0'))
            {
                withEpsilon.Transitions.Add(new Transition
                {
                    FromStateId = fromState.Id,
                    ToStateId = toState.Id,
                    Symbol = '\0'
                });

                logger.LogInformation("Added epsilon transition: q{From} --ε--> q{To}", fromState.Id, toState.Id);
            }
        }

        logger.LogInformation("Successfully created ε-NFA with {EpsilonCount} epsilon transitions",
            withEpsilon.Transitions.Count(t => t.Symbol == '\0'));

        return withEpsilon;
    }

    public AutomatonViewModel GenerateEpsilonNfaNondeterministic(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null)
    {
        logger.LogInformation("Generating nondeterministic ε-NFA preset with {StateCount} states", stateCount);

        // Generate a base ε-NFA with epsilon transitions
        var baseEnfa = GenerateEpsilonNfa(stateCount, transitionCount, alphabetSize, acceptingRatio, seed);

        // Check if it's already nondeterministic
        if (IsNondeterministic(baseEnfa))
        {
            logger.LogInformation("Generated ε-NFA is already nondeterministic");
            return baseEnfa;
        }

        // Add nondeterministic transitions (including possibly epsilon-based nondeterminism)
        logger.LogInformation("Generated ε-NFA is deterministic, adding nondeterministic transitions");
        return AddNondeterministicTransitions(baseEnfa, seed);
    }
}
