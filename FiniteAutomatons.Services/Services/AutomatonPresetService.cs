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
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating minimalized DFA preset with {StateCount} states", stateCount);
        }
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
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Successfully generated minimalized DFA with {OriginalStates} -> {MinimizedStates} states",
                dfa.States.Count, minModel.States.Count);
        }
        return minModel;
    }

    public AutomatonViewModel GenerateRandomPda(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null, PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null, AutomatonType pdaType = AutomatonType.DPDA)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating random {PdaType} preset with {StateCount} states, AcceptanceMode: {Mode}", pdaType, stateCount, acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack);
        }
        return generatorService.GenerateRandomAutomaton(pdaType, stateCount, transitionCount, alphabetSize, acceptingRatio, seed, acceptanceMode, initialStack);
    }

    public AutomatonViewModel GeneratePdaWithPushPopPairs(int stateCount = 5, int transitionCount = 12, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null, PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null, AutomatonType pdaType = AutomatonType.DPDA)
    {
        LogPdaGenerationStart(stateCount, acceptanceMode, pdaType);

        var basePda = generatorService.GenerateRandomAutomaton(pdaType, stateCount, transitionCount,
            alphabetSize, acceptingRatio, seed, acceptanceMode, initialStack);

        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var alphabet = ExtractAlphabet(basePda);

        if (alphabet.Count == 0)
            return basePda;

        AddPushPopPairs(basePda, alphabet, random, enforceDeterminism: pdaType == AutomatonType.DPDA);

        LogPdaGenerationComplete(basePda);
        return basePda;
    }

    private static void AddPushPopPairs(AutomatonViewModel pda, List<char> alphabet, Random random, bool enforceDeterminism)
    {
        int added = 0;
        int maxPairs = Math.Min(3, alphabet.Count);

        for (int i = 0; i < maxPairs && added < 6; i++)
        {
            var (pushSymbol, popSymbol) = SelectDifferentSymbols(alphabet, random);
            if (pushSymbol == popSymbol)
                continue;

            if (enforceDeterminism)
            {
                if (TryAddPushTransitionDeterministic(pda, pushSymbol, random))
                {
                    added++;
                }

                if (TryAddPopTransitionDeterministic(pda, popSymbol, pushSymbol, random))
                {
                    added++;
                }
            }
            else
            {
                AddPushTransition(pda, pushSymbol, random);
                AddPopTransition(pda, popSymbol, pushSymbol, random);
                added += 2;
            }
        }
    }

    private static bool TryAddPushTransitionDeterministic(AutomatonViewModel pda, char symbol, Random random)
    {
        const int maxAttempts = 24;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var transition = CreatePushTransition(pda, symbol, random);
            if (!CanAddDeterministically(pda.Transitions, transition))
                continue;

            pda.Transitions.Add(transition);
            return true;
        }

        return false;
    }

    private static bool TryAddPopTransitionDeterministic(AutomatonViewModel pda, char symbol, char stackSymbol, Random random)
    {
        const int maxAttempts = 24;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var transition = CreatePopTransition(pda, symbol, stackSymbol, random);
            if (!CanAddDeterministically(pda.Transitions, transition))
                continue;

            pda.Transitions.Add(transition);
            return true;
        }

        return false;
    }

    private static Transition CreatePushTransition(AutomatonViewModel pda, char symbol, Random random)
    {
        var from = pda.States[random.Next(pda.States.Count)].Id;
        var to = pda.States[random.Next(pda.States.Count)].Id;

        return new Transition
        {
            FromStateId = from,
            ToStateId = to,
            Symbol = symbol,
            StackPop = null,
            StackPush = symbol.ToString()
        };
    }

    private static Transition CreatePopTransition(AutomatonViewModel pda, char symbol, char stackSymbol, Random random)
    {
        var from = pda.States[random.Next(pda.States.Count)].Id;
        var to = pda.States[random.Next(pda.States.Count)].Id;

        return new Transition
        {
            FromStateId = from,
            ToStateId = to,
            Symbol = symbol,
            StackPop = stackSymbol,
            StackPush = null
        };
    }

    private static (char push, char pop) SelectDifferentSymbols(List<char> alphabet, Random random)
    {
        var pushSym = alphabet[random.Next(alphabet.Count)];
        var popSym = alphabet[random.Next(alphabet.Count)];
        return (pushSym, popSym);
    }

    private static void AddPushTransition(AutomatonViewModel pda, char symbol, Random random)
    {
        pda.Transitions.Add(CreatePushTransition(pda, symbol, random));
    }

    private static void AddPopTransition(AutomatonViewModel pda, char symbol, char stackSymbol, Random random)
    {
        pda.Transitions.Add(CreatePopTransition(pda, symbol, stackSymbol, random));
    }

    private static bool CanAddDeterministically(IEnumerable<Transition> transitions, Transition candidate)
    {
        foreach (var existing in transitions.Where(t => t.FromStateId == candidate.FromStateId))
        {
            if (!StackConditionsOverlap(existing, candidate))
                continue;

            bool existingIsEpsilon = existing.Symbol == '\0';
            bool candidateIsEpsilon = candidate.Symbol == '\0';

            if (existing.Symbol == candidate.Symbol)
                return false;

            if (existingIsEpsilon ^ candidateIsEpsilon)
                return false;
        }

        return true;
    }

    private static bool StackConditionsOverlap(Transition t1, Transition t2)
    {
        bool t1AnyTop = !t1.StackPop.HasValue || t1.StackPop.Value == '\0';
        bool t2AnyTop = !t2.StackPop.HasValue || t2.StackPop.Value == '\0';

        if (t1AnyTop || t2AnyTop)
            return true;

        return t1.StackPop!.Value == t2.StackPop!.Value;
    }

    private static List<char> ExtractAlphabet(AutomatonViewModel automaton)
    {
        return [.. automaton.Transitions
            .Where(t => t.Symbol != '\0')
            .Select(t => t.Symbol)
            .Distinct()];
    }

    private void LogPdaGenerationStart(int stateCount, PDAAcceptanceMode? acceptanceMode, AutomatonType pdaType)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating {PdaType} preset with push/pop pairs, states={StateCount}, AcceptanceMode: {Mode}",
                pdaType, stateCount, acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack);
        }
    }

    private void LogPdaGenerationComplete(AutomatonViewModel pda)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generated PDA preset with {Transitions} transitions (including push/pop)",
                pda.Transitions.Count);
        }
    }

    public AutomatonViewModel GenerateBalancedParenthesesPda(PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating balanced parentheses PDA with AcceptanceMode: {Mode}", acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack);
        }
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            IsCustomAutomaton = true,
            AcceptanceMode = acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack,
            InitialStackSerialized = SerializeStack(initialStack)
        };

        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = '(', StackPop = '\0', StackPush = "(" });
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
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating a^n b^n PDA with AcceptanceMode: {Mode}", acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack);
        }
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true },  // q0: push a's
                new() { Id = 2, IsStart = false, IsAccepting = true }  // q1: pop b's
            ],
            Transitions = [],
            IsCustomAutomaton = true,
            AcceptanceMode = acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack,
            InitialStackSerialized = SerializeStack(initialStack)
        };

        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = null, StackPush = "X" });
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'b', StackPop = 'X', StackPush = null });
        model.Transitions.Add(new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'b', StackPop = 'X', StackPush = null });

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generated a^n b^n PDA with {States} states and {Transitions} transitions",
            model.States.Count, model.Transitions.Count);
        }
        return model;
    }

    public AutomatonViewModel GenerateEvenPalindromePda(PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating even-length palindrome PDA with AcceptanceMode: {Mode}",
            acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack);
        }
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },  // q0
                new() { Id = 2, IsStart = false, IsAccepting = true }   // q1
            ],
            Transitions = [],
            IsCustomAutomaton = true,
            AcceptanceMode = acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack,
            InitialStackSerialized = SerializeStack(initialStack)
        };

        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = null, StackPush = "a" });
        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = null, StackPush = "b" });

        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 2, Symbol = '\0', StackPop = null, StackPush = null });

        model.Transitions.Add(new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'a', StackPop = 'a', StackPush = null });
        model.Transitions.Add(new Transition { FromStateId = 2, ToStateId = 2, Symbol = 'b', StackPop = 'b', StackPush = null });
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generated even-length palindrome PDA with {States} states and {Transitions} transitions",
            model.States.Count, model.Transitions.Count);
        }
        return model;
    }

    public AutomatonViewModel GenerateSimpleCfgPda(PDAAcceptanceMode? acceptanceMode = null, Stack<char>? initialStack = null)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating simple CFG demo PDA (S → aSb | ε) with AcceptanceMode: {Mode}",
            acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack);
        }

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true }
            ],
            Transitions = [],
            IsCustomAutomaton = true,
            AcceptanceMode = acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack,
            InitialStackSerialized = initialStack != null
                ? SerializeStack(initialStack)
                : System.Text.Json.JsonSerializer.Serialize(new List<char> { 'S' })
        };

        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = 'S', StackPush = "Sb" });

        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = '\0', StackPop = 'S', StackPush = null });

        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = null, StackPush = "a" });

        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'b', StackPush = null });

        model.Transitions.Clear();

        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = null, StackPush = "a" });

        model.Transitions.Add(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'a', StackPush = null });
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generated simple CFG demo PDA with {States} states and {Transitions} transitions",
            model.States.Count, model.Transitions.Count);
        }
        return model;
    }

    public AutomatonViewModel GenerateUnminimalizedDfa(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating unminimalized DFA preset with {StateCount} states", stateCount);
        }
        var baseDfa = generatorService.GenerateRandomAutomaton(AutomatonType.DFA, stateCount, transitionCount, alphabetSize, acceptingRatio, seed);

        var (minimized, _) = minimizationService.MinimizeDfa(baseDfa);

        if (minimized != null && minimized.States.Count < baseDfa.States.Count)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Generated DFA is already unminimalized ({OriginalStates} -> {MinimalStates} states)",
                baseDfa.States.Count, minimized.States.Count);
            }
            return baseDfa;
        }

        logger.LogInformation("Generated DFA is minimal, adding equivalent states to create unminimalized version");
        return AddEquivalentStates(baseDfa, seed);
    }

    private AutomatonViewModel AddEquivalentStates(AutomatonViewModel dfa, int? seed)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var unminimalized = CloneAutomaton(dfa);

        var candidateStates = SelectCandidateStates(unminimalized);
        if (candidateStates.Count == 0)
        {
            logger.LogWarning("Cannot add equivalent states - DFA has only start state");
            return unminimalized;
        }

        var statesToDuplicate = Math.Min(2, candidateStates.Count);
        var nextStateId = unminimalized.States.Max(s => s.Id) + 1;

        for (int i = 0; i < statesToDuplicate; i++)
        {
            var originalState = candidateStates[random.Next(candidateStates.Count)];
            nextStateId = DuplicateState(unminimalized, originalState, nextStateId);
        }

        LogUnminimalizationComplete(dfa.States.Count, statesToDuplicate);
        return unminimalized;
    }

    private static AutomatonViewModel CloneAutomaton(AutomatonViewModel source)
    {
        return new AutomatonViewModel
        {
            Type = source.Type,
            States = [.. source.States],
            Transitions = [.. source.Transitions],
            Input = source.Input,
            IsCustomAutomaton = source.IsCustomAutomaton
        };
    }

    private static List<State> SelectCandidateStates(AutomatonViewModel automaton)
    {
        var candidates = automaton.States
            .Where(s => !s.IsStart && !s.IsAccepting)
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = [.. automaton.States.Where(s => !s.IsStart)];
        }

        return candidates;
    }

    private int DuplicateState(AutomatonViewModel automaton, State originalState, int nextStateId)
    {
        var equivalentState = CreateEquivalentState(originalState, nextStateId);
        automaton.States.Add(equivalentState);

        CopyOutgoingTransitions(automaton, originalState.Id, equivalentState.Id);
        LogStateDuplication(equivalentState.Id, originalState.Id);

        return nextStateId + 1;
    }

    private static State CreateEquivalentState(State original, int newId)
    {
        return new State
        {
            Id = newId,
            IsStart = false,
            IsAccepting = original.IsAccepting
        };
    }

    private static void CopyOutgoingTransitions(AutomatonViewModel automaton, int fromStateId, int toNewStateId)
    {
        var outgoingTransitions = automaton.Transitions
            .Where(t => t.FromStateId == fromStateId)
            .ToList();

        foreach (var transition in outgoingTransitions)
        {
            automaton.Transitions.Add(new Transition
            {
                FromStateId = toNewStateId,
                ToStateId = transition.ToStateId,
                Symbol = transition.Symbol
            });
        }
    }

    private void LogStateDuplication(int equivalentId, int originalId)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Added equivalent state q{EquivalentId} for q{OriginalId}",
                equivalentId, originalId);
        }
    }

    private void LogUnminimalizationComplete(int originalCount, int addedCount)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Successfully created unminimalized DFA with {OriginalStates} + {AddedStates} states",
                originalCount, addedCount);
        }
    }

    public AutomatonViewModel GenerateNondeterministicNfa(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating nondeterministic NFA preset with {StateCount} states", stateCount);
        }

        var baseNfa = generatorService.GenerateRandomAutomaton(AutomatonType.NFA, stateCount, transitionCount, alphabetSize, acceptingRatio, seed);

        if (IsNondeterministic(baseNfa))
        {
            logger.LogInformation("Generated NFA is already nondeterministic");
            return baseNfa;
        }

        logger.LogInformation("Generated NFA is deterministic, adding nondeterministic transitions");
        return AddNondeterministicTransitions(baseNfa, seed);
    }

    public AutomatonViewModel GenerateNondeterministicEpsilonNfa(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating nondeterministic epsilon-NFA preset with {StateCount} states", stateCount);
        }

        var baseeNfa = generatorService.GenerateRandomAutomaton(AutomatonType.EpsilonNFA, stateCount, transitionCount, alphabetSize, acceptingRatio, seed);

        if (IsNondeterministic(baseeNfa))
        {
            logger.LogInformation("Generated eNFA is already nondeterministic");
            return baseeNfa;
        }

        logger.LogInformation("Generated Epsilon-NFA is deterministic, adding nondeterministic transitions");
        return AddNondeterministicTransitions(baseeNfa, seed);
    }

    private static bool IsNondeterministic(AutomatonViewModel nfa)
    {
        var transitionGroups = nfa.Transitions
            .GroupBy(t => new { t.FromStateId, t.Symbol })
            .Where(g => g.Count() > 1);

        return transitionGroups.Any();
    }

    private AutomatonViewModel AddNondeterministicTransitions(AutomatonViewModel nfa, int? seed)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var nondetNfa = CloneAutomaton(nfa);

        var context = PrepareNondeterministicContext(nondetNfa);
        if (!context.IsValid)
            return nondetNfa;

        AddNondeterministicTransitionsLoop(nondetNfa, context, random);
        LogNondeterministicResult(context);

        return nondetNfa;
    }

    private NondeterministicContext PrepareNondeterministicContext(AutomatonViewModel nfa)
    {
        var alphabet = ExtractAlphabet(nfa);
        if (alphabet.Count == 0)
        {
            logger.LogWarning("Cannot add nondeterministic transitions - no alphabet symbols");
            return NondeterministicContext.Invalid();
        }

        var statesWithTransitions = nfa.Transitions
            .Select(t => t.FromStateId)
            .Distinct()
            .ToList();

        if (statesWithTransitions.Count == 0)
        {
            logger.LogWarning("Cannot add nondeterministic transitions - no states with transitions");
            return NondeterministicContext.Invalid();
        }

        var maxAttempts = Math.Max(50, statesWithTransitions.Count * alphabet.Count * 2);
        var desiredTransitions = Math.Min(3, statesWithTransitions.Count);

        return new NondeterministicContext
        {
            IsValid = true,
            Alphabet = alphabet,
            StatesWithTransitions = statesWithTransitions,
            MaxAttempts = maxAttempts,
            DesiredTransitions = desiredTransitions
        };
    }

    private void AddNondeterministicTransitionsLoop(AutomatonViewModel nfa, NondeterministicContext context, Random random)
    {
        while (context.Added < context.DesiredTransitions && context.Attempts < context.MaxAttempts)
        {
            context.Attempts++;

            var fromState = context.StatesWithTransitions[random.Next(context.StatesWithTransitions.Count)];
            var symbol = context.Alphabet[random.Next(context.Alphabet.Count)];

            var existingTargets = GetExistingTargets(nfa, fromState, symbol);
            var availableTargets = GetAvailableTargets(nfa, existingTargets);

            if (availableTargets.Count > 0)
            {
                AddNondeterministicTransition(nfa, fromState, symbol, availableTargets, random);
                context.Added++;
            }
        }
    }

    private static HashSet<int> GetExistingTargets(AutomatonViewModel nfa, int fromState, char symbol)
    {
        return [.. nfa.Transitions
            .Where(t => t.FromStateId == fromState && t.Symbol == symbol)
            .Select(t => t.ToStateId)];
    }

    private static List<int> GetAvailableTargets(AutomatonViewModel nfa, HashSet<int> existingTargets)
    {
        return [.. nfa.States
            .Select(s => s.Id)
            .Where(id => !existingTargets.Contains(id))];
    }

    private void AddNondeterministicTransition(AutomatonViewModel nfa, int fromState, char symbol,
        List<int> availableTargets, Random random)
    {
        var toState = availableTargets[random.Next(availableTargets.Count)];

        nfa.Transitions.Add(new Transition
        {
            FromStateId = fromState,
            ToStateId = toState,
            Symbol = symbol
        });

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Added nondeterministic transition: q{From} --{Symbol}--> q{To}",
                fromState, symbol, toState);
        }
    }

    private void LogNondeterministicResult(NondeterministicContext context)
    {
        if (context.Added == 0)
        {
            logger.LogWarning("Failed to add any nondeterministic transitions after {Attempts} attempts",
                context.Attempts);
        }
        else if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Successfully created nondeterministic NFA: added {Added} transitions in {Attempts} attempts",
                context.Added, context.Attempts);
        }
    }

    private class NondeterministicContext
    {
        public bool IsValid { get; init; }
        public List<char> Alphabet { get; init; } = [];
        public List<int> StatesWithTransitions { get; init; } = [];
        public int MaxAttempts { get; init; }
        public int DesiredTransitions { get; init; }
        public int Added { get; set; }
        public int Attempts { get; set; }

        public static NondeterministicContext Invalid() => new() { IsValid = false };
    }

    public AutomatonViewModel GenerateRandomDfa(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating random DFA preset with {StateCount} states", stateCount);
        }
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
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating random NFA preset with {StateCount} states", stateCount);
        }
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
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating ε-NFA preset with epsilon transitions, {StateCount} states", stateCount);
        }
        var baseEnfa = generatorService.GenerateRandomAutomaton(
            AutomatonType.EpsilonNFA,
            stateCount,
            transitionCount,
            alphabetSize,
            acceptingRatio,
            seed);

        var hasEpsilonTransitions = baseEnfa.Transitions.Any(t => t.Symbol == '\0');

        if (hasEpsilonTransitions)
        {
            logger.LogInformation("Generated ε-NFA already has epsilon transitions");
            return baseEnfa;
        }

        logger.LogInformation("Generated ε-NFA has no epsilon transitions, adding them");
        return AddEpsilonTransitions(baseEnfa, seed);
    }

    private AutomatonViewModel AddEpsilonTransitions(AutomatonViewModel enfa, int? seed)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var withEpsilon = CloneAutomaton(enfa);
        withEpsilon.Type = AutomatonType.EpsilonNFA;

        if (!ValidateStateCountForEpsilon(withEpsilon))
            return withEpsilon;

        var context = CreateEpsilonContext(withEpsilon);
        AddEpsilonTransitionsLoop(withEpsilon, context, random);
        LogEpsilonTransitionResult(withEpsilon, context);

        return withEpsilon;
    }

    private bool ValidateStateCountForEpsilon(AutomatonViewModel automaton)
    {
        if (automaton.States.Count >= 2)
            return true;

        logger.LogWarning("Cannot add epsilon transitions - need at least 2 states");
        return false;
    }

    private static EpsilonContext CreateEpsilonContext(AutomatonViewModel automaton)
    {
        var transitionsToAdd = Math.Min(3, automaton.States.Count);
        var maxAttempts = Math.Max(10, transitionsToAdd * 10);

        return new EpsilonContext
        {
            TransitionsToAdd = transitionsToAdd,
            MaxAttempts = maxAttempts
        };
    }

    private void AddEpsilonTransitionsLoop(AutomatonViewModel automaton, EpsilonContext context, Random random)
    {
        while (context.Added < context.TransitionsToAdd && context.Attempts < context.MaxAttempts)
        {
            context.Attempts++;

            var fromState = automaton.States[random.Next(automaton.States.Count)];
            var toState = automaton.States[random.Next(automaton.States.Count)];

            if (!IsValidEpsilonTransition(automaton, fromState, toState))
                continue;

            AddEpsilonTransition(automaton, fromState.Id, toState.Id);
            context.Added++;
        }
    }

    private static bool IsValidEpsilonTransition(AutomatonViewModel automaton, State fromState, State toState)
    {
        if (fromState.Id == toState.Id)
            return false;

        return !automaton.Transitions.Any(t =>
            t.FromStateId == fromState.Id &&
            t.ToStateId == toState.Id &&
            t.Symbol == '\0');
    }

    private void AddEpsilonTransition(AutomatonViewModel automaton, int fromStateId, int toStateId)
    {
        automaton.Transitions.Add(new Transition
        {
            FromStateId = fromStateId,
            ToStateId = toStateId,
            Symbol = '\0'
        });

        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Added epsilon transition: q{From} --ε--> q{To}", fromStateId, toStateId);
        }
    }

    private void LogEpsilonTransitionResult(AutomatonViewModel automaton, EpsilonContext context)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            var totalEpsilon = automaton.Transitions.Count(t => t.Symbol == '\0');
            logger.LogInformation("Successfully created ε-NFA: attempted {Attempts}, added {Added} epsilon transitions (total now {Total})",
                context.Attempts, context.Added, totalEpsilon);
        }
    }

    private class EpsilonContext
    {
        public int TransitionsToAdd { get; init; }
        public int MaxAttempts { get; init; }
        public int Added { get; set; }
        public int Attempts { get; set; }
    }

    public AutomatonViewModel GenerateEpsilonNfaNondeterministic(int stateCount = 5, int transitionCount = 10, int alphabetSize = 3, double acceptingRatio = 0.3, int? seed = null)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating nondeterministic ε-NFA preset with {StateCount} states", stateCount);
        }

        var baseEnfa = GenerateEpsilonNfa(stateCount, transitionCount, alphabetSize, acceptingRatio, seed);

        if (IsNondeterministic(baseEnfa))
        {
            logger.LogInformation("Generated ε-NFA is already nondeterministic");
            return baseEnfa;
        }

        logger.LogInformation("Generated ε-NFA is deterministic, adding nondeterministic transitions");
        var result = AddNondeterministicTransitions(baseEnfa, seed);

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
        var alphabet = ExtractAlphabet(automaton);

        if (!ValidateForceNondeterminism(alphabet, automaton))
            return automaton;

        if (TryDuplicateExistingTransition(automaton, random))
            return automaton;

        AddRandomNondeterministicTransition(automaton, alphabet, random);
        return automaton;
    }

    private bool ValidateForceNondeterminism(List<char> alphabet, AutomatonViewModel automaton)
    {
        if (alphabet.Count > 0 && automaton.States.Count >= 2)
            return true;

        logger.LogWarning("Cannot force nondeterministic transition - insufficient alphabet or states");
        return false;
    }

    private bool TryDuplicateExistingTransition(AutomatonViewModel automaton, Random random)
    {
        var existingTransitions = automaton.Transitions.Where(t => t.Symbol != '\0').ToList();
        if (existingTransitions.Count == 0)
            return false;

        var baseTransition = existingTransitions[random.Next(existingTransitions.Count)];
        var otherStates = automaton.States.Where(s => s.Id != baseTransition.ToStateId).ToList();

        if (otherStates.Count == 0)
            return false;

        var newTarget = otherStates[random.Next(otherStates.Count)];
        automaton.Transitions.Add(new Transition
        {
            FromStateId = baseTransition.FromStateId,
            ToStateId = newTarget.Id,
            Symbol = baseTransition.Symbol
        });

        LogForcedTransition(baseTransition.FromStateId, baseTransition.Symbol, newTarget.Id);
        return true;
    }

    private void AddRandomNondeterministicTransition(AutomatonViewModel automaton, List<char> alphabet, Random random)
    {
        var fromState = automaton.States[random.Next(automaton.States.Count)];
        var symbol = alphabet[random.Next(alphabet.Count)];
        var toState = automaton.States[random.Next(automaton.States.Count)];

        automaton.Transitions.Add(new Transition
        {
            FromStateId = fromState.Id,
            ToStateId = toState.Id,
            Symbol = symbol
        });

        LogForcedTransition(fromState.Id, symbol, toState.Id);
    }

    private void LogForcedTransition(int fromStateId, char symbol, int toStateId)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Force-added nondeterministic transition: q{From} --{Symbol}--> q{To}",
                fromStateId, symbol, toStateId);
        }
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
