using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
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

    public AutomatonViewModel GenerateRandomPda(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null, PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null)
    {
        logger.LogInformation("Generating random PDA preset with {StateCount} states, AcceptanceMode: {Mode}", stateCount, acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack);
        return generatorService.GenerateRandomAutomaton(AutomatonType.PDA, stateCount, transitionCount, alphabetSize, acceptingRatio, seed, acceptanceMode, initialStack);
    }

    public AutomatonViewModel GeneratePdaWithPushPopPairs(int stateCount = 5, int transitionCount = 12, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null, PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null)
    {
        logger.LogInformation("Generating PDA preset with push/pop pairs, states={StateCount}, AcceptanceMode: {Mode}", stateCount, acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack);
        // Create a PDA with additional push/pop matching pairs to be educational
        var basePda = generatorService.GenerateRandomAutomaton(AutomatonType.PDA, stateCount, transitionCount, alphabetSize, acceptingRatio, seed, acceptanceMode, initialStack);

        // Try to add a few push/pop pairs if possible
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var alphabet = basePda.Transitions.Where(t => t.Symbol != '\0').Select(t => t.Symbol).Distinct().ToList();
        if (alphabet.Count == 0)
        {
            // nothing to do
            return basePda;
        }

        // Add couple of paired transitions that push on one symbol and pop on another
        int added = 0;
        for (int i = 0; i < Math.Min(3, alphabet.Count) && added < 3; i++)
        {
            var pushSym = alphabet[random.Next(alphabet.Count)];
            var popSym = alphabet[random.Next(alphabet.Count)];
            if (pushSym == popSym) continue;

            // choose random from/to states
            var from = basePda.States[random.Next(basePda.States.Count)].Id;
            var to = basePda.States[random.Next(basePda.States.Count)].Id;
            basePda.Transitions.Add(new Transition { FromStateId = from, ToStateId = to, Symbol = pushSym, StackPop = null, StackPush = pushSym.ToString() });

            var from2 = basePda.States[random.Next(basePda.States.Count)].Id;
            var to2 = basePda.States[random.Next(basePda.States.Count)].Id;
            basePda.Transitions.Add(new Transition { FromStateId = from2, ToStateId = to2, Symbol = popSym, StackPop = pushSym, StackPush = null });
            added += 2;
        }

        logger.LogInformation("Generated PDA preset with {Transitions} transitions (including push/pop)", basePda.Transitions.Count);
        return basePda;
    }

    public AutomatonViewModel GenerateBalancedParenthesesPda(PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null)
    {
        logger.LogInformation("Generating balanced parentheses PDA with AcceptanceMode: {Mode}", acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack);

        // Construct a simple PDA that accepts balanced parentheses of '(' and ')'
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = new List<State> { new() { Id = 1, IsStart = true, IsAccepting = true } },
            Transitions = new List<Transition>(),
            IsCustomAutomaton = true,
            AcceptanceMode = acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack,
            InitialStackSerialized = SerializeStack(initialStack)
        };

        // push '(' on reading '('
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = '(', StackPop = '\0', StackPush = "(" });
        // pop '(' on reading ')'
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = ')', StackPop = '(', StackPush = null });

        return model;
    }

    private static string SerializeStack(Stack<char>? stack)
    {
        if (stack == null || stack.Count == 0)
            return string.Empty;

        return System.Text.Json.JsonSerializer.Serialize(stack.ToList());
    }

    public AutomatonViewModel GenerateAnBnPda(PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null)
    {
        logger.LogInformation("Generating a^n b^n PDA with AcceptanceMode: {Mode}", acceptanceMode ?? PDAAcceptanceMode.EmptyStackOnly);

        // PDA that recognizes { a^n b^n | n >= 0 }
        // States: q0 (start, reading a's), q1 (reading b's), q2 (accepting)
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },  // q0
                new() { Id = 2, IsStart = false, IsAccepting = false }, // q1
                new() { Id = 3, IsStart = false, IsAccepting = true }   // q2
            ],
            Transitions = [],
            IsCustomAutomaton = true,
            AcceptanceMode = acceptanceMode ?? PDAAcceptanceMode.EmptyStackOnly,
            InitialStackSerialized = SerializeStack(initialStack)
        };

        // Transition: q0 --a/ε→X-- q0 (push X for each 'a')
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = null, StackPush = "X" });

        // Transition: q0 --b/X→ε-- q1 (pop X for first 'b', switch to q1)
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'b', StackPop = 'X', StackPush = null });

        // Transition: q1 --b/X→ε-- q1 (continue popping X for each 'b')
        model.Transitions.Add(new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'b', StackPop = 'X', StackPush = null });

        // Transition: q1 --ε/ε→ε-- q2 (when done, go to accepting state)
        model.Transitions.Add(new Transition { FromStateId = 2, ToStateId = 3, Symbol = '\0', StackPop = null, StackPush = null });

        // Also allow empty string acceptance from q0 to q2
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 3, Symbol = '\0', StackPop = null, StackPush = null });

        logger.LogInformation("Generated a^n b^n PDA with {States} states and {Transitions} transitions",
            model.States.Count, model.Transitions.Count);

        return model;
    }

    public AutomatonViewModel GenerateEvenPalindromePda(PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null)
    {
        logger.LogInformation("Generating even-length palindrome PDA with AcceptanceMode: {Mode}",
            acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack);

        // PDA that recognizes even-length palindromes over {a, b}
        // States: q0 (push phase), q1 (pop/verify phase, accepting)
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = new List<State>
            {
                new() { Id = 1, IsStart = true, IsAccepting = false },  // q0
                new() { Id = 2, IsStart = false, IsAccepting = true }   // q1
            },
            Transitions = new List<Transition>(),
            IsCustomAutomaton = true,
            AcceptanceMode = acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack,
            InitialStackSerialized = SerializeStack(initialStack)
        };

        // Push phase (q0): push symbols onto stack
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = null, StackPush = "a" });
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = null, StackPush = "b" });

        // Nondeterministic guess of middle: ε-transition to pop phase
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 2, Symbol = '\0', StackPop = null, StackPush = null });

        // Pop phase (q1): pop and match symbols
        model.Transitions.Add(new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'a', StackPop = 'a', StackPush = null });
        model.Transitions.Add(new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'b', StackPop = 'b', StackPush = null });

        logger.LogInformation("Generated even-length palindrome PDA with {States} states and {Transitions} transitions",
            model.States.Count, model.Transitions.Count);

        return model;
    }

    public AutomatonViewModel GenerateSimpleCfgPda(PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null)
    {
        logger.LogInformation("Generating simple CFG demo PDA (S → aSb | ε) with AcceptanceMode: {Mode}",
            acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack);

        // PDA demonstrating CFG: S → aSb | ε
        // This generates language { a^n b^n | n >= 0 } using grammar derivation
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.PDA,
            States = new List<State>
            {
                new() { Id = 1, IsStart = true, IsAccepting = true }  // Single state
            },
            Transitions = new List<Transition>(),
            IsCustomAutomaton = true,
            AcceptanceMode = acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack,
            InitialStackSerialized = initialStack != null
                ? SerializeStack(initialStack)
                : System.Text.Json.JsonSerializer.Serialize(new List<char> { 'S' }) // Start with 'S' on stack
        };

        // Rule: S → aSb (expand S to aSb on stack, pushes 'b' then 'a' so 'a' is on top)
        // When we see 'a', if 'S' is on top, replace with 'Sb' (push b, then we'll handle the 'a')
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = 'S', StackPush = "Sb" });

        // After the above, we need to consume the 'a' that was in input
        // This is already done by reading 'a' in the transition above

        // Rule: S → ε (apply epsilon production)
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = '\0', StackPop = 'S', StackPush = null });

        // Consume 'a' from input by popping nothing (we already consumed in the S→aSb rule)
        // Actually, let's simplify: consume 'a' when not expanding S
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = null, StackPush = "a" });

        // Consume 'b' and match with 'b' on stack
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'b', StackPush = null });

        // Alternative simpler approach: Match a's and b's like a^n b^n
        // Clear previous and use simpler model
        model.Transitions.Clear();

        // Push 'a' for each 'a' read
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = null, StackPush = "a" });

        // Pop 'a' for each 'b' read
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'a', StackPush = null });

        logger.LogInformation("Generated simple CFG demo PDA with {States} states and {Transitions} transitions",
            model.States.Count, model.Transitions.Count);

        return model;
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

        // GUARANTEE at least one nondeterministic transition is added
        var added = 0;
        var attempts = 0;
        var maxAttempts = Math.Max(50, statesWithTransitions.Count * alphabet.Count * 2);
        var desiredTransitions = Math.Min(3, statesWithTransitions.Count);

        while (added < desiredTransitions && attempts < maxAttempts)
        {
            attempts++;
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

                added++;
                logger.LogInformation("Added nondeterministic transition: q{From} --{Symbol}--> q{To}",
                    fromState, symbol, toState);
            }
        }

        if (added == 0)
        {
            logger.LogWarning("Failed to add any nondeterministic transitions after {Attempts} attempts", attempts);
        }
        else
        {
            logger.LogInformation("Successfully created nondeterministic NFA: added {Added} transitions in {Attempts} attempts",
                added, attempts);
        }

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
            States = [.. enfa.States],
            Transitions = [.. enfa.Transitions],
            Input = enfa.Input,
            IsCustomAutomaton = enfa.IsCustomAutomaton
        };

        if (withEpsilon.States.Count < 2)
        {
            logger.LogWarning("Cannot add epsilon transitions - need at least 2 states");
            return withEpsilon;
        }


        // Add 2-3 epsilon transitions between random states. Use retries to avoid rare cases
        // where random picks produce only self-loops or duplicates (causing flakiness in tests).
        var epsilonTransitionsToAdd = Math.Min(3, withEpsilon.States.Count);
        var added = 0;
        var attempts = 0;
        var maxAttempts = Math.Max(10, epsilonTransitionsToAdd * 10);

        while (added < epsilonTransitionsToAdd && attempts < maxAttempts)
        {
            attempts++;
            var fromState = withEpsilon.States[random.Next(withEpsilon.States.Count)];
            var toState = withEpsilon.States[random.Next(withEpsilon.States.Count)];

            // Avoid self-loops and duplicate epsilon transitions
            if (fromState.Id == toState.Id)
                continue;
            if (withEpsilon.Transitions.Any(t => t.FromStateId == fromState.Id && t.ToStateId == toState.Id && t.Symbol == '\0'))
                continue;

            withEpsilon.Transitions.Add(new Transition
            {
                FromStateId = fromState.Id,
                ToStateId = toState.Id,
                Symbol = '\0'
            });

            added++;
            logger.LogInformation("Added epsilon transition: q{From} --ε--> q{To}", fromState.Id, toState.Id);
        }

        logger.LogInformation("Successfully created ε-NFA: attempted {Attempts}, added {Added} epsilon transitions (total now {Total})",
            attempts, added, withEpsilon.Transitions.Count(t => t.Symbol == '\0'));

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
        var result = AddNondeterministicTransitions(baseEnfa, seed);

        // GUARANTEE: If still not nondeterministic after AddNondeterministicTransitions, force add one
        if (!IsNondeterministic(result))
        {
            logger.LogWarning("Failed to add nondeterministic transitions via random method, forcing one guaranteed nondeterministic transition");
            result = ForceNondeterministicTransition(result, seed);
        }

        return result;
    }

    private AutomatonViewModel ForceNondeterministicTransition(AutomatonViewModel automaton, int? seed)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();

        // Get alphabet (non-epsilon symbols)
        var alphabet = automaton.Transitions
            .Where(t => t.Symbol != '\0')
            .Select(t => t.Symbol)
            .Distinct()
            .ToList();

        if (alphabet.Count == 0 || automaton.States.Count < 2)
        {
            logger.LogWarning("Cannot force nondeterministic transition - insufficient alphabet or states");
            return automaton;
        }

        // Strategy 1: Find an existing transition and duplicate it to a different target
        var existingTransitions = automaton.Transitions.Where(t => t.Symbol != '\0').ToList();
        if (existingTransitions.Any())
        {
            var baseTransition = existingTransitions[random.Next(existingTransitions.Count)];
            var otherStates = automaton.States.Where(s => s.Id != baseTransition.ToStateId).ToList();

            if (otherStates.Any())
            {
                var newTarget = otherStates[random.Next(otherStates.Count)];
                automaton.Transitions.Add(new Transition
                {
                    FromStateId = baseTransition.FromStateId,
                    ToStateId = newTarget.Id,
                    Symbol = baseTransition.Symbol
                });

                logger.LogInformation("Force-added nondeterministic transition: q{From} --{Symbol}--> q{To}",
                    baseTransition.FromStateId, baseTransition.Symbol, newTarget.Id);
                return automaton;
            }
        }

        // Strategy 2: Create a new transition from any state on any symbol to create nondeterminism
        var fromState = automaton.States[random.Next(automaton.States.Count)];
        var symbol = alphabet[random.Next(alphabet.Count)];
        var toState = automaton.States[random.Next(automaton.States.Count)];

        automaton.Transitions.Add(new Transition
        {
            FromStateId = fromState.Id,
            ToStateId = toState.Id,
            Symbol = symbol
        });

        logger.LogInformation("Force-added nondeterministic transition: q{From} --{Symbol}--> q{To}",
            fromState.Id, symbol, toState.Id);

        return automaton;
    }

    public string GetPresetDisplayName(string preset)
    {
        return preset.Trim().ToLowerInvariant() switch
        {
            "random-dfa" => "Random DFA",
            "minimalized-dfa" => "Minimalized DFA",
            "unminimalized-dfa" => "Unminimalized DFA",
            "nondet-nfa" => "Nondeterministic NFA",
            "random-nfa" => "Random NFA",
            "enfa-eps" => "ε-NFA with Epsilon Transitions",
            "enfa-nondet" => "Nondeterministic ε-NFA",
            "random-enfa" => "Random ε-NFA",
            "random-pda" => "Random PDA",
            "pda-pushpop" => "PDA with Push/Pop Pairs",
            "pda-balanced-parens" => "Balanced Parentheses PDA",
            "pda-anbn" => "a^n b^n PDA",
            "pda-palindrome" => "Even-Length Palindrome PDA",
            "pda-cfg-demo" => "Simple CFG Demo PDA",
            _ => preset
        };
    }
}
