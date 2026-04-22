using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;

namespace FiniteAutomatons.Services.Services;

public class AutomatonGeneratorService : IAutomatonGeneratorService
{
    private readonly Random random;

    public AutomatonGeneratorService()
    {
        random = new Random();
    }

    public AutomatonViewModel GenerateRandomAutomaton(
        AutomatonType type,
        int stateCount,
        int transitionCount,
        int alphabetSize = 3,
        double acceptingStateRatio = 0.3,
        int? seed = null,
        PDAAcceptanceMode? acceptanceMode = null,
        Stack<char>? initialStack = null)
    {
        if (acceptingStateRatio < 0.0 || acceptingStateRatio > 1.0)
        {
            throw new ArgumentOutOfRangeException(nameof(acceptingStateRatio), "Accepting state ratio must be between 0.0 and 1.0.");
        }

        if (!ValidateGenerationParameters(type, stateCount, transitionCount, alphabetSize))
        {
            throw new ArgumentException("Invalid generation parameters");
        }

        var random = seed.HasValue ? new Random(seed.Value) : this.random;

        var alphabet = GenerateAlphabet(alphabetSize);
        var states = GenerateStates(stateCount, acceptingStateRatio, random);
        var effectiveTransitionCount = type == AutomatonType.NPDA
            ? Math.Max(2, transitionCount)
            : transitionCount;

        var transitions = GenerateTransitions(type, states, effectiveTransitionCount, alphabet, random);

        var viewModel = new AutomatonViewModel
        {
            Type = type,
            States = states,
            Transitions = transitions,
            Input = "",
            IsCustomAutomaton = true
        };

        if (type == AutomatonType.DPDA || type == AutomatonType.NPDA)
        {
            viewModel.AcceptanceMode = acceptanceMode ?? PDAAcceptanceMode.FinalStateAndEmptyStack;
            viewModel.InitialStackSerialized = SerializeStack(initialStack);
        }

        return viewModel;
    }

    private static string SerializeStack(Stack<char>? stack)
    {
        if (stack == null || stack.Count == 0)
            return string.Empty;

        return System.Text.Json.JsonSerializer.Serialize(stack.ToList());
    }

    public bool ValidateGenerationParameters(AutomatonType type, int stateCount, int transitionCount, int alphabetSize)
    {
        if (stateCount < 1)
            return false;

        if (transitionCount < 0)
            return false;

        // Require enough transitions to keep all states reachable from the start state.
        if (stateCount > 1 && transitionCount < stateCount - 1)
            return false;

        if (alphabetSize < 1)
            return false;

        if (type == AutomatonType.DFA && transitionCount > stateCount * alphabetSize)
            return false;

        return true;
    }

    public (int stateCount, int transitionCount, int alphabetSize, double acceptingRatio) GenerateRandomParameters(int? seed = null)
    {
        var random = seed.HasValue ? new Random(seed.Value) : this.random;
        var stateCount = random.Next(5, 16);           // 5-15 states
        var minTransitions = Math.Max(4, stateCount - 1);
        var transitionCount = random.Next(minTransitions, 26); // minTransitions-25 transitions
        var alphabetSize = random.Next(2, 9);          // 2-8 alphabet size
        var acceptingRatio = 0.2 + random.NextDouble() * 0.3; // 0.2-0.5

        return (stateCount, transitionCount, alphabetSize, acceptingRatio);
    }

    private static List<char> GenerateAlphabet(int size)
    {
        var alphabet = new List<char>();
        for (int i = 0; i < size; i++)
        {
            alphabet.Add((char)('a' + i));
        }
        return alphabet;
    }

    private static List<State> GenerateStates(int count, double acceptingRatio, Random random)
    {
        var states = new List<State>();
        int acceptingCount = acceptingRatio <= 0.0
            ? 0
            : Math.Min(count, Math.Max(1, (int)Math.Round(count * acceptingRatio, MidpointRounding.AwayFromZero)));

        for (int i = 1; i <= count; i++)
        {
            states.Add(new State
            {
                Id = i,
                IsStart = i == 1,
                IsAccepting = false
            });
        }

        var acceptingIndices = Enumerable.Range(0, count)
            .OrderBy(_ => random.Next())
            .Take(acceptingCount)
            .ToList();

        foreach (var index in acceptingIndices)
        {
            states[index].IsAccepting = true;
        }

        return states;
    }

    private static List<Transition> GenerateTransitions(
        AutomatonType type,
        List<State> states,
        int transitionCount,
        List<char> alphabet,
        Random random)
    {
        var context = new TransitionGenerationContext
        {
            Type = type,
            States = states,
            Alphabet = alphabet,
            Random = random,
            Transitions = [],
            AddedTransitions = [],
            Budget = Math.Max(0, transitionCount)
        };

        EnsureGraphConnectivity(context);
        EnsureAlphabetCoverage(context);
        FillRemainingBudget(context);
        GuaranteeAllSymbolsPresent(context, transitionCount);

        if (type == AutomatonType.NPDA)
        {
            EnsureNpdaNondeterminism(context);
        }

        return context.Transitions;
    }

    private static void EnsureNpdaNondeterminism(TransitionGenerationContext context)
    {
        if (HasNpdaNondeterminism(context.Transitions) || context.Transitions.Count == 0)
        {
            return;
        }

        var source = context.Transitions[context.Random.Next(context.Transitions.Count)];
        var branch = new Transition
        {
            FromStateId = source.FromStateId,
            ToStateId = source.ToStateId,
            Symbol = source.Symbol,
            StackPop = source.StackPop,
            StackPush = source.StackPush
        };

        var alternateTarget = context.States
            .Select(s => s.Id)
            .FirstOrDefault(id => id != source.ToStateId);

        if (alternateTarget != 0)
        {
            branch.ToStateId = alternateTarget;
        }
        else
        {
            branch.StackPush = string.IsNullOrEmpty(source.StackPush)
                ? $"{context.Alphabet[0]}{context.Alphabet[0]}"
                : $"{source.StackPush}{context.Alphabet[0]}";
        }

        AddOrReplaceNpdaBranch(context, branch);
    }

    private static bool HasNpdaNondeterminism(List<Transition> transitions)
    {
        return transitions
            .GroupBy(t => (t.FromStateId, t.Symbol, t.StackPop))
            .Any(g => g.Count() > 1);
    }

    private static void AddOrReplaceNpdaBranch(TransitionGenerationContext context, Transition branch)
    {
        var key = GetTransitionKey(branch, context.Type);

        if (context.AddedTransitions.Contains(key))
        {
            branch.StackPush = string.IsNullOrEmpty(branch.StackPush)
                ? context.Alphabet[context.Random.Next(context.Alphabet.Count)].ToString()
                : $"{branch.StackPush}{context.Alphabet[context.Random.Next(context.Alphabet.Count)]}";

            key = GetTransitionKey(branch, context.Type);
        }

        if (context.AddedTransitions.Contains(key))
        {
            var replace = context.Transitions[^1];
            context.AddedTransitions.Remove(GetTransitionKey(replace, context.Type));
            context.Transitions[^1] = branch;
            context.AddedTransitions.Add(GetTransitionKey(branch, context.Type));
            return;
        }

        context.Transitions.Add(branch);
        context.AddedTransitions.Add(key);
    }

    private static void EnsureGraphConnectivity(TransitionGenerationContext context)
    {
        if (context.Budget >= context.States.Count - 1 && context.States.Count > 1)
        {
            int added = EnsureConnectivity(context.Type, context.States, context.Alphabet,
                context.Transitions, context.AddedTransitions, context.Random, context.Budget);
            context.Budget -= added;
        }
    }

    private static void EnsureAlphabetCoverage(TransitionGenerationContext context)
    {
        if (context.Budget > 0)
        {
            int added = EnsureAllSymbolsPresent(context.Type, context.States, context.Alphabet,
                context.Transitions, context.AddedTransitions, context.Random, context.Budget);
            context.Budget -= added;
        }
    }

    private static void FillRemainingBudget(TransitionGenerationContext context)
    {
        for (int i = 0; i < context.Budget; i++)
        {
            var transition = GenerateRandomTransition(context.Type, context.States, context.Alphabet,
                context.Transitions, context.AddedTransitions, context.Random);

            if (transition == null)
                break;

            context.Transitions.Add(transition);
            context.AddedTransitions.Add(GetTransitionKey(transition, context.Type));
        }
    }

    private static void GuaranteeAllSymbolsPresent(TransitionGenerationContext context, int maxTotal)
    {
        var presentSymbols = context.Transitions
            .Select(t => t.Symbol)
            .Where(c => c != '\0')
            .ToHashSet();

        foreach (var symbol in context.Alphabet)
        {
            if (context.Transitions.Count >= maxTotal && presentSymbols.Contains(symbol))
                continue;

            if (presentSymbols.Contains(symbol))
                continue;

            TryAddMissingSymbol(context, symbol, maxTotal, presentSymbols);
        }
    }

    private static void TryAddMissingSymbol(TransitionGenerationContext context, char symbol, int maxTotal, HashSet<char> presentSymbols)
    {
        const int maxAttempts = 50;

        if (TryAddNewTransitionForSymbol(context, symbol, maxTotal, maxAttempts, presentSymbols))
            return;

        if (context.Transitions.Count >= maxTotal)
        {
            TryReplaceTransitionToIncludeSymbol(context, symbol, presentSymbols);
        }
    }

    private static bool TryAddNewTransitionForSymbol(TransitionGenerationContext context, char symbol,
        int maxTotal, int maxAttempts, HashSet<char> presentSymbols)
    {
        for (int attempt = 0; attempt < maxAttempts && context.Transitions.Count < maxTotal; attempt++)
        {
            var candidate = CreateTransitionCandidate(context, symbol);
            if (!CanAddTransition(context.Type, candidate, context.Transitions, context.AddedTransitions))
                continue;

            var key = GetTransitionKey(candidate, context.Type);

            context.Transitions.Add(candidate);
            context.AddedTransitions.Add(key);
            presentSymbols.Add(symbol);
            return true;
        }

        return false;
    }

    private static void TryReplaceTransitionToIncludeSymbol(TransitionGenerationContext context, char symbol, HashSet<char> presentSymbols)
    {
        var symbolCounts = context.Transitions
            .GroupBy(t => t.Symbol)
            .ToDictionary(g => g.Key, g => g.Count());

        var replaceable = context.Transitions.FirstOrDefault(t =>
            symbolCounts.TryGetValue(t.Symbol, out var count) && count > 1);

        if (replaceable == null)
            return;

        var newCandidate = new Transition
        {
            FromStateId = replaceable.FromStateId,
            ToStateId = replaceable.ToStateId,
            Symbol = symbol
        };

        if (context.Type == AutomatonType.DPDA || context.Type == AutomatonType.NPDA)
        {
            var useStackOperation = context.Random.NextDouble() > 0.3; 
            if (useStackOperation && context.Alphabet.Count > 0)
            {
                var isPop = context.Random.NextDouble() > 0.5;
                if (isPop)
                {
                    newCandidate.StackPop = context.Alphabet[context.Random.Next(context.Alphabet.Count)];
                    newCandidate.StackPush = "";
                }
                else
                {
                    newCandidate.StackPop = null;
                    newCandidate.StackPush = context.Alphabet[context.Random.Next(context.Alphabet.Count)].ToString();
                }
            }
        }

        var newKey = GetTransitionKey(newCandidate, context.Type);
        var oldKey = GetTransitionKey(replaceable, context.Type);
        var keysWithoutOld = new HashSet<string>(context.AddedTransitions);
        keysWithoutOld.Remove(oldKey);
        var remainingTransitions = context.Transitions.Where(t => !ReferenceEquals(t, replaceable)).ToList();

        if (!CanAddTransition(context.Type, newCandidate, remainingTransitions, keysWithoutOld))
            return;

        context.AddedTransitions.Remove(oldKey);
        context.AddedTransitions.Add(newKey);
        context.Transitions.Remove(replaceable);
        context.Transitions.Add(newCandidate);
        presentSymbols.Add(symbol);
    }

    private static Transition CreateTransitionCandidate(TransitionGenerationContext context, char symbol)
    {
        var fromState = context.States[context.Random.Next(context.States.Count)].Id;
        var toState = context.States[context.Random.Next(context.States.Count)].Id;
        var candidate = new Transition
        {
            FromStateId = fromState,
            ToStateId = toState,
            Symbol = symbol
        };

        if (context.Type == AutomatonType.DPDA || context.Type == AutomatonType.NPDA)
        {
            var useStackOperation = context.Random.NextDouble() > 0.3; // 70% chance to use stack op
            if (useStackOperation && context.Alphabet.Count > 0)
            {
                var isPop = context.Random.NextDouble() > 0.5;
                if (isPop)
                {
                    candidate.StackPop = context.Alphabet[context.Random.Next(context.Alphabet.Count)];
                    candidate.StackPush = "";
                }
                else
                {
                    candidate.StackPop = null;
                    candidate.StackPush = context.Alphabet[context.Random.Next(context.Alphabet.Count)].ToString();
                }
            }
        }

        return candidate;
    }

    private class TransitionGenerationContext
    {
        public AutomatonType Type { get; init; }
        public List<State> States { get; init; } = null!;
        public List<char> Alphabet { get; init; } = null!;
        public Random Random { get; init; } = null!;
        public List<Transition> Transitions { get; init; } = null!;
        public HashSet<string> AddedTransitions { get; init; } = null!;
        public int Budget { get; set; }
    }

    private static int EnsureConnectivity(
        AutomatonType type,
        List<State> states,
        List<char> alphabet,
        List<Transition> transitions,
        HashSet<string> addedTransitions,
        Random random,
        int maxToAdd)
    {
        int added = 0;
        for (int i = 0; i < states.Count - 1 && added < maxToAdd; i++)
        {
            var fromState = states[i].Id;
            var toState = states[i + 1].Id;
            var symbol = alphabet[random.Next(alphabet.Count)];

            var transition = new Transition
            {
                FromStateId = fromState,
                ToStateId = toState,
                Symbol = symbol
            };

            if (type == AutomatonType.DPDA || type == AutomatonType.NPDA)
            {
                transition.StackPop = null;
                transition.StackPush = symbol.ToString();
            }

            if (CanAddTransition(type, transition, transitions, addedTransitions))
            {
                var key = GetTransitionKey(transition, type);
                transitions.Add(transition);
                addedTransitions.Add(key);
                added++;
            }
        }

        if ((type == AutomatonType.DPDA || type == AutomatonType.NPDA) && added < maxToAdd)
        {
            int addedPairs = CreateMatchingPushPopPairs(type, states, alphabet, transitions, addedTransitions, random, maxToAdd - added);
            added += addedPairs;
        }

        return added;
    }

    private static int CreateMatchingPushPopPairs(AutomatonType type, List<State> states, List<char> alphabet,
        List<Transition> transitions, HashSet<string> addedTransitions, Random random, int maxToAdd)
    {
        int pairCount = CalculatePairCount(alphabet.Count);
        int added = 0;

        for (int i = 0; i < pairCount && added < maxToAdd; i++)
        {
            var (pushInput, popInput) = SelectPushPopSymbols(alphabet, random);
            char stackSymbol = GenerateStackSymbol(i);

            added += TryAddPushTransition(type, states, transitions, addedTransitions, random,
                maxToAdd, added, pushInput, stackSymbol);

            added += TryAddPopTransition(type, states, transitions, addedTransitions, random,
                maxToAdd, added, popInput, stackSymbol);
        }

        return added;
    }

    private static int CalculatePairCount(int alphabetSize)
    {
        int pairCount = Math.Max(1, alphabetSize / 2);
        return Math.Min(pairCount, Math.Max(1, alphabetSize));
    }

    private static (char pushInput, char popInput) SelectPushPopSymbols(List<char> alphabet, Random random)
    {
        char pushInput = alphabet[random.Next(alphabet.Count)];
        char popInput = alphabet[random.Next(alphabet.Count)];

        if (alphabet.Count > 1)
        {
            int attempts = 0;
            while (popInput == pushInput && attempts++ < 10)
            {
                popInput = alphabet[random.Next(alphabet.Count)];
            }
        }

        return (pushInput, popInput);
    }

    private static char GenerateStackSymbol(int index)
    {
        return (char)('A' + (index % 26));
    }

    private static int TryAddPushTransition(AutomatonType type, List<State> states, List<Transition> transitions,
        HashSet<string> addedTransitions, Random random, int maxToAdd, int currentAdded,
        char pushInput, char stackSymbol)
    {
        if (currentAdded >= maxToAdd)
            return 0;

        var fromState = states[random.Next(states.Count)].Id;
        var toState = states[random.Next(states.Count)].Id;

        var pushTransition = new Transition
        {
            FromStateId = fromState,
            ToStateId = toState,
            Symbol = pushInput,
            StackPop = null,
            StackPush = stackSymbol.ToString()
        };

        var key = GetTransitionKey(pushTransition, type);
        if (!CanAddTransition(type, pushTransition, transitions, addedTransitions))
            return 0;

        transitions.Add(pushTransition);
        addedTransitions.Add(key);
        return 1;
    }

    private static int TryAddPopTransition(AutomatonType type, List<State> states, List<Transition> transitions,
        HashSet<string> addedTransitions, Random random, int maxToAdd, int currentAdded,
        char popInput, char stackSymbol)
    {
        if (currentAdded >= maxToAdd)
            return 0;

        var fromState = states[random.Next(states.Count)].Id;
        var toState = states[random.Next(states.Count)].Id;

        var popTransition = new Transition
        {
            FromStateId = fromState,
            ToStateId = toState,
            Symbol = popInput,
            StackPop = stackSymbol,
            StackPush = null
        };

        var key = GetTransitionKey(popTransition, type);
        if (!CanAddTransition(type, popTransition, transitions, addedTransitions))
            return 0;

        transitions.Add(popTransition);
        addedTransitions.Add(key);
        return 1;
    }

    private static int EnsureAllSymbolsPresent(
        AutomatonType type,
        List<State> states,
        List<char> alphabet,
        List<Transition> transitions,
        HashSet<string> addedTransitions,
        Random random,
        int maxToAdd)
    {
        var present = transitions.Select(t => t.Symbol).Where(c => c != '\0').ToHashSet();
        int added = 0;
        foreach (var symbol in alphabet)
        {
            if (present.Contains(symbol)) continue;
            if (added >= maxToAdd) break;

            const int maxAttempts = 100;
            for (int attempt = 0; attempt < maxAttempts && added < maxToAdd; attempt++)
            {
                var fromState = states[random.Next(states.Count)].Id;
                var toState = states[random.Next(states.Count)].Id;
                var candidate = new Transition { FromStateId = fromState, ToStateId = toState, Symbol = symbol };

                if (type == AutomatonType.DPDA || type == AutomatonType.NPDA)
                {
                    candidate.StackPop = null;
                    candidate.StackPush = symbol.ToString();
                }

                if (!CanAddTransition(type, candidate, transitions, addedTransitions)) continue;

                var key = GetTransitionKey(candidate, type);

                transitions.Add(candidate);
                addedTransitions.Add(key);
                added++;
                break;
            }
        }

        return added;
    }

    private static Transition? GenerateRandomTransition(
        AutomatonType type,
        List<State> states,
        List<char> alphabet,
        List<Transition> transitions,
        HashSet<string> addedTransitions,
        Random random)
    {
        const int maxAttempts = 200;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var transition = CreateRandomTransitionCandidate(type, states, alphabet, random);
            if (!CanAddTransition(type, transition, transitions, addedTransitions))
                continue;

            return transition;
        }

        return null;
    }

    private static Transition CreateRandomTransitionCandidate(AutomatonType type, List<State> states,
        List<char> alphabet, Random random)
    {
        var fromState = states[random.Next(states.Count)].Id;
        var toState = states[random.Next(states.Count)].Id;
        var symbol = SelectTransitionSymbol(type, alphabet, random);

        var transition = new Transition
        {
            FromStateId = fromState,
            ToStateId = toState,
            Symbol = symbol
        };

        if (type == AutomatonType.DPDA || type == AutomatonType.NPDA)
        {
            AssignPdaStackOperations(transition, alphabet, random);
        }

        return transition;
    }

    private static char SelectTransitionSymbol(AutomatonType type, List<char> alphabet, Random random)
    {
        if (type == AutomatonType.EpsilonNFA && random.NextDouble() < 0.2)
        {
            return '\0';
        }

        return alphabet[random.Next(alphabet.Count)];
    }

    private static void AssignPdaStackOperations(Transition transition, List<char> alphabet, Random random)
    {
        // Stack pop operation
        if (random.NextDouble() < 0.5)
        {
            transition.StackPop = null;
        }
        else
        {
            transition.StackPop = alphabet[random.Next(alphabet.Count)];
        }

        // Stack push operation
        if (random.NextDouble() < 0.6)
        {
            transition.StackPush = null;
        }
        else
        {
            transition.StackPush = GenerateStackPushString(alphabet, random);
        }
    }

    private static string GenerateStackPushString(List<char> alphabet, Random random)
    {
        int length = random.Next(1, 3);
        var builder = new System.Text.StringBuilder(length);

        for (int i = 0; i < length; i++)
        {
            builder.Append(alphabet[random.Next(alphabet.Count)]);
        }

        return builder.ToString();
    }

    private static bool CanAddTransition(AutomatonType type, Transition candidate,
        List<Transition> transitions, HashSet<string> addedTransitions)
    {
        var key = GetTransitionKey(candidate, type);
        if (addedTransitions.Contains(key))
            return false;

        if (type == AutomatonType.DFA)
        {
            return !transitions.Any(t => t.FromStateId == candidate.FromStateId && t.Symbol == candidate.Symbol);
        }

        if (type == AutomatonType.DPDA)
        {
            // Formal DPDA determinism: conflicts only when stack-top conditions overlap
            // for same input symbol, or epsilon/consuming transitions overlap.
            return !HasDpdaConflict(transitions, candidate);
        }

        return true;
    }

    private static bool HasDpdaConflict(List<Transition> transitions, Transition candidate)
    {
        foreach (var existing in transitions.Where(t => t.FromStateId == candidate.FromStateId))
        {
            if (!StackConditionsOverlap(existing.StackPop, candidate.StackPop))
                continue;

            if (existing.Symbol == candidate.Symbol)
                return true;

            var existingIsEpsilon = existing.Symbol == '\0';
            var candidateIsEpsilon = candidate.Symbol == '\0';
            if (existingIsEpsilon ^ candidateIsEpsilon)
                return true;
        }

        return false;
    }

    private static bool StackConditionsOverlap(char? firstStackPop, char? secondStackPop)
    {
        if (!firstStackPop.HasValue || firstStackPop.Value == '\0')
            return true;

        if (!secondStackPop.HasValue || secondStackPop.Value == '\0')
            return true;

        return firstStackPop.Value == secondStackPop.Value;
    }

    private static string GetTransitionKey(Transition transition, AutomatonType type)
    {
        if (type == AutomatonType.DFA)
        {
            return $"{transition.FromStateId}-{transition.Symbol}";
        }

        var pop = transition.StackPop.HasValue ? transition.StackPop.Value.ToString() : "_";
        if (type == AutomatonType.DPDA)
        {
            // Keep DPDA deterministic on (state, symbol, stack-pop).
            return $"{transition.FromStateId}-{transition.Symbol}-{pop}";
        }

        var push = string.IsNullOrEmpty(transition.StackPush) ? "_" : transition.StackPush;
        // For NFA/ENFA/NPDA, allow nondeterministic branching and only dedupe exact duplicates.
        return $"{transition.FromStateId}-{transition.ToStateId}-{transition.Symbol}-{pop}-{push}";
    }
}
