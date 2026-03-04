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
        if (!ValidateGenerationParameters(type, stateCount, transitionCount, alphabetSize))
        {
            throw new ArgumentException("Invalid generation parameters");
        }

        var random = seed.HasValue ? new Random(seed.Value) : this.random;

        var alphabet = GenerateAlphabet(alphabetSize);
        var states = GenerateStates(stateCount, acceptingStateRatio, random);
        var transitions = GenerateTransitions(type, states, transitionCount, alphabet, random);

        var viewModel = new AutomatonViewModel
        {
            Type = type,
            States = states,
            Transitions = transitions,
            Input = "",
            IsCustomAutomaton = true
        };

        if (type == AutomatonType.PDA)
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
        var transitionCount = random.Next(4, 26);      // 4-25 transitions
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
        int acceptingCount = Math.Max(1, (int)(count * acceptingRatio));

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
        var transitions = new List<Transition>();
        var addedTransitions = new HashSet<string>();

        int budget = Math.Max(0, transitionCount);

        if (budget >= states.Count - 1 && states.Count > 1)
        {
            int added = EnsureConnectivity(type, states, alphabet, transitions, addedTransitions, random, budget);
            budget -= added;
        }

        if (budget > 0)
        {
            int added = EnsureAllSymbolsPresent(type, states, alphabet, transitions, addedTransitions, random, budget);
            budget -= added;
        }

        for (int i = 0; i < budget; i++)
        {
            var transition = GenerateRandomTransition(type, states, alphabet, addedTransitions, random);
            if (transition != null)
            {
                transitions.Add(transition);
                addedTransitions.Add(GetTransitionKey(transition));
            }
            else
            {
                break;
            }
        }

        var maxTotal = Math.Max(0, transitionCount);
        var present = transitions.Select(t => t.Symbol).Where(c => c != '\0').ToHashSet();
        foreach (var symbol in alphabet)
        {
            if (transitions.Count >= maxTotal && present.Contains(symbol)) continue;
            if (present.Contains(symbol)) continue;
            const int maxAttempts = 50;
            bool added = false;
            for (int attempt = 0; attempt < maxAttempts && !added && transitions.Count < maxTotal; attempt++)
            {
                var fromState = states[random.Next(states.Count)].Id;
                var toState = states[random.Next(states.Count)].Id;
                var candidate = new Transition { FromStateId = fromState, ToStateId = toState, Symbol = symbol };
                if (type == AutomatonType.PDA)
                {
                    candidate.StackPop = null;
                    candidate.StackPush = symbol.ToString();
                }
                var key = GetTransitionKey(candidate);
                if (addedTransitions.Contains(key)) continue;
                if (type == AutomatonType.DFA && transitions.Any(t => t.FromStateId == fromState && t.Symbol == symbol)) continue;
                transitions.Add(candidate);
                addedTransitions.Add(key);
                present.Add(symbol);
                added = true;
            }
            if (!added && transitions.Count >= maxTotal)
            {
                var symbolCounts = transitions.GroupBy(t => t.Symbol).ToDictionary(g => g.Key, g => g.Count());
                var replacable = transitions.FirstOrDefault(t => symbolCounts.TryGetValue(t.Symbol, out var c) && c > 1);
                if (replacable != null)
                {
                    var newCandidate = new Transition { FromStateId = replacable.FromStateId, ToStateId = replacable.ToStateId, Symbol = symbol };
                    if (type == AutomatonType.PDA)
                    {
                        newCandidate.StackPop = null;
                        newCandidate.StackPush = symbol.ToString();
                    }
                    var newKey = GetTransitionKey(newCandidate);
                    if (!addedTransitions.Contains(newKey))
                    {
                        var oldKey = GetTransitionKey(replacable);
                        addedTransitions.Remove(oldKey);
                        addedTransitions.Add(newKey);
                        transitions.Remove(replacable);
                        transitions.Add(newCandidate);
                        present.Add(symbol);
                    }
                }
            }
        }

        return transitions;
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

            if (type == AutomatonType.PDA)
            {
                transition.StackPop = null;
                transition.StackPush = symbol.ToString();
            }

            var key = GetTransitionKey(transition);
            if (!addedTransitions.Contains(key))
            {
                transitions.Add(transition);
                addedTransitions.Add(key);
                added++;
            }
        }

        if (type == AutomatonType.PDA && added < maxToAdd)
        {
            int addedPairs = CreateMatchingPushPopPairs(states, alphabet, transitions, addedTransitions, random, maxToAdd - added);
            added += addedPairs;
        }

        return added;
    }

    private static int CreateMatchingPushPopPairs(List<State> states, List<char> alphabet, List<Transition> transitions, HashSet<string> addedTransitions, Random random, int maxToAdd)
    {
        int pairCount = Math.Max(1, alphabet.Count / 2);
        pairCount = Math.Min(pairCount, Math.Max(1, alphabet.Count));

        int added = 0;
        for (int i = 0; i < pairCount && added < maxToAdd; i++)
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

            char stackSym = (char)('A' + (i % 26));
            string stackPushStr = stackSym.ToString();

            var fromPush = states[random.Next(states.Count)].Id;
            var toPush = states[random.Next(states.Count)].Id;
            var pushTrans = new Transition
            {
                FromStateId = fromPush,
                ToStateId = toPush,
                Symbol = pushInput,
                StackPop = null,
                StackPush = stackPushStr
            };
            var keyPush = GetTransitionKey(pushTrans);
            if (!addedTransitions.Contains(keyPush) && added < maxToAdd)
            {
                transitions.Add(pushTrans);
                addedTransitions.Add(keyPush);
                added++;
            }

            var fromPop = states[random.Next(states.Count)].Id;
            var toPop = states[random.Next(states.Count)].Id;
            var popTrans = new Transition
            {
                FromStateId = fromPop,
                ToStateId = toPop,
                Symbol = popInput,
                StackPop = stackSym,
                StackPush = null
            };
            var keyPop = GetTransitionKey(popTrans);
            if (!addedTransitions.Contains(keyPop) && added < maxToAdd)
            {
                transitions.Add(popTrans);
                addedTransitions.Add(keyPop);
                added++;
            }
        }

        return added;
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

                if (type == AutomatonType.PDA)
                {
                    candidate.StackPop = null;
                    candidate.StackPush = symbol.ToString();
                }

                var key = GetTransitionKey(candidate);
                if (addedTransitions.Contains(key)) continue;

                if (type == AutomatonType.DFA)
                {
                    if (transitions.Any(t => t.FromStateId == fromState && t.Symbol == symbol))
                        continue;
                }

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
        HashSet<string> addedTransitions,
        Random random)
    {
        int maxAttempts = 200;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var fromState = states[random.Next(states.Count)].Id;
            var toState = states[random.Next(states.Count)].Id;

            char symbol;

            if (type == AutomatonType.EpsilonNFA && random.NextDouble() < 0.2)
            {
                symbol = '\0';
            }
            else
            {
                symbol = alphabet[random.Next(alphabet.Count)];
            }

            var transition = new Transition
            {
                FromStateId = fromState,
                ToStateId = toState,
                Symbol = symbol
            };

            if (type == AutomatonType.PDA)
            {
                if (random.NextDouble() < 0.5)
                {
                    transition.StackPop = null;
                }
                else
                {
                    transition.StackPop = alphabet[random.Next(alphabet.Count)];
                }

                if (random.NextDouble() < 0.6)
                {
                    transition.StackPush = null;
                }
                else
                {
                    int len = random.Next(1, 3);
                    var sb = new System.Text.StringBuilder();
                    for (int i = 0; i < len; i++) sb.Append(alphabet[random.Next(alphabet.Count)]);
                    transition.StackPush = sb.ToString();
                }
            }

            var key = GetTransitionKey(transition);

            if (addedTransitions.Contains(key))
                continue;

            if (type == AutomatonType.DFA && symbol != '\0')
            {
                var conflictExists = addedTransitions.Any(existing =>
                {
                    var parts = existing.Split('-');
                    return parts.Length >= 2 && parts[0] == fromState.ToString() && parts[1] == symbol.ToString();
                });

                if (conflictExists)
                    continue;
            }

            return transition;
        }

        return null;
    }

    private static string GetTransitionKey(Transition transition)
    {
        var pop = transition.StackPop.HasValue ? transition.StackPop.Value.ToString() : "_";
        return $"{transition.FromStateId}-{transition.Symbol}-{pop}";
    }
}
