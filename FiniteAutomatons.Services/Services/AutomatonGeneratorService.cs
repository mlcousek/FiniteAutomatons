using FiniteAutomatons.Core.Models.DoMain;
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
        int? seed = null)
    {
        if (!ValidateGenerationParameters(type, stateCount, transitionCount, alphabetSize))
        {
            throw new ArgumentException("Invalid generation parameters");
        }

        var random = seed.HasValue ? new Random(seed.Value) : this.random;

        var alphabet = GenerateAlphabet(alphabetSize);
        var states = GenerateStates(stateCount, acceptingStateRatio, random);
        var transitions = GenerateTransitions(type, states, transitionCount, alphabet, random);

        return new AutomatonViewModel
        {
            Type = type,
            States = states,
            Transitions = transitions,
            Input = "",
            IsCustomAutomaton = true
        };
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

        // Respect the requested transitionCount strictly. We may optionally try to ensure
        // connectivity and alphabet coverage but only as budget allows. This makes it possible
        // to request fewer transitions than number of states (resulting in states without transitions).
        int budget = Math.Max(0, transitionCount);

        // Try to ensure basic connectivity only if there's enough budget to add at least (states.Count - 1) edges
        // and user requested enough transitions. Otherwise skip to allow sparse graphs.
        if (budget >= states.Count - 1 && states.Count > 1)
        {
            int added = EnsureConnectivity(type, states, alphabet, transitions, addedTransitions, random, budget);
            budget -= added;
        }

        // Ensure each alphabet symbol appears at least once only if we still have budget
        if (budget > 0)
        {
            int added = EnsureAllSymbolsPresent(type, states, alphabet, transitions, addedTransitions, random, budget);
            budget -= added;
        }

        // Fill remaining budget with random transitions (may be zero)
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
                break; // cannot generate more unique transitions
            }
        }

        // Make a best-effort to ensure every alphabet symbol appears at least once in transitions.
        // Some earlier steps (connectivity, push/pop pairs) may have consumed the budget and left
        // some symbols unused; add extra transitions for any missing symbols if possible.
        var maxTotal = Math.Max(0, transitionCount);
        var present = transitions.Select(t => t.Symbol).Where(c => c != '\0').ToHashSet();
        foreach (var symbol in alphabet)
        {
            if (transitions.Count >= maxTotal && present.Contains(symbol)) continue;
            if (present.Contains(symbol)) continue;
            // try a few times to add a transition for this symbol
            const int maxAttempts = 50;
            bool added = false;
            for (int attempt = 0; attempt < maxAttempts && !added && transitions.Count < maxTotal; attempt++)
            {
                var fromState = states[random.Next(states.Count)].Id;
                var toState = states[random.Next(states.Count)].Id;
                var candidate = new Transition { FromStateId = fromState, ToStateId = toState, Symbol = symbol };
                if (type == AutomatonType.PDA)
                {
                    // basic PDA candidate: no pop, push the input symbol
                    candidate.StackPop = null;
                    candidate.StackPush = symbol.ToString();
                }
                var key = GetTransitionKey(candidate);
                if (addedTransitions.Contains(key)) continue;
                // respect DFA uniqueness if needed
                if (type == AutomatonType.DFA && transitions.Any(t => t.FromStateId == fromState && t.Symbol == symbol)) continue;
                transitions.Add(candidate);
                addedTransitions.Add(key);
                present.Add(symbol);
                added = true;
            }
            // If we couldn't add because we are at maxTotal, try to replace an existing transition that has a duplicate symbol
            if (!added && transitions.Count >= maxTotal)
            {
                // find a transition whose symbol occurs more than once and which can be safely replaced
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
                        // replace
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
                // For connectivity, create simple PDA moves: push on symbol (no pop)
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

        // If PDA, additionally create some matching push/pop pairs to produce useful PDA patterns
        if (type == AutomatonType.PDA && added < maxToAdd)
        {
            // Allow PDA extra pairs but bounded by remaining budget
            int addedPairs = CreateMatchingPushPopPairs(states, alphabet, transitions, addedTransitions, random, maxToAdd - added);
            added += addedPairs;
        }

        return added;
    }

    private static int CreateMatchingPushPopPairs(List<State> states, List<char> alphabet, List<Transition> transitions, HashSet<string> addedTransitions, Random random, int maxToAdd)
    {
        // Decide number of pairs: at most alphabet.Count / 2, at least 1
        int pairCount = Math.Max(1, alphabet.Count / 2);
        pairCount = Math.Min(pairCount, Math.Max(1, alphabet.Count));

        // Use distinct stack symbols for each pair (uppercase letters)
        int added = 0;
        for (int i = 0; i < pairCount && added < maxToAdd; i++)
        {
            // pick two distinct input symbols to act as push and pop triggers
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

            // choose a stack symbol (uppercase letter A..Z based on i)
            char stackSym = (char)('A' + (i % 26));
            string stackPushStr = stackSym.ToString();

            // create a push transition (no stack pop condition)
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

            // create a pop transition (requires stackSym on top)
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
                    // create a basic PDA candidate: either push or pop pattern
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
                // Decide stackPop: either null (no condition) or a symbol from alphabet
                if (random.NextDouble() < 0.5)
                {
                    transition.StackPop = null;
                }
                else
                {
                    transition.StackPop = alphabet[random.Next(alphabet.Count)];
                }

                // Decide stackPush: either null (no push) or a short string of 1-2 symbols
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
        // Use FromStateId + Symbol + StackPop as uniqueness key to enforce deterministic PDA constraints
        var pop = transition.StackPop.HasValue ? transition.StackPop.Value.ToString() : "_";
        return $"{transition.FromStateId}-{transition.Symbol}-{pop}";
    }
}
