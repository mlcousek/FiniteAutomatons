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

    public AutomatonViewModel GenerateRealisticAutomaton(
        AutomatonType type,
        int stateCount,
        int? seed = null)
    {
        if (stateCount < 1)
        {
            throw new ArgumentException("State count must be at least 1");
        }

        var random = seed.HasValue ? new Random(seed.Value) : this.random;

        int alphabetSize = Math.Min(3 + random.Next(3), 6); 
        int minTransitions = stateCount; 
        int maxTransitions = Math.Min(stateCount * alphabetSize, stateCount * stateCount); 
        int transitionCount = random.Next(minTransitions, Math.Max(minTransitions + 1, maxTransitions / 2));
        
        if (type == AutomatonType.EpsilonNFA)
        {
            transitionCount += random.Next(1, Math.Max(2, stateCount / 3));
        }

        double acceptingRatio = 0.2 + random.NextDouble() * 0.3; 

        return GenerateRandomAutomaton(type, stateCount, transitionCount, alphabetSize, acceptingRatio, seed);
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

    #region Private Helper Methods

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

        // 1. Ensure basic connectivity
        EnsureConnectivity(states, alphabet, transitions, addedTransitions, random);

        // 2. Ensure each alphabet symbol appears at least once (non-epsilon)
        EnsureAllSymbolsPresent(type, states, alphabet, transitions, addedTransitions, random);

        // 3. Fill remaining transitions up to requested count (may already exceed if we had to force missing symbols)
        int remainingTransitions = transitionCount - transitions.Count;
        for (int i = 0; i < remainingTransitions; i++)
        {
            var transition = GenerateRandomTransition(type, states, alphabet, addedTransitions, random);
            if (transition != null)
            {
                transitions.Add(transition);
                addedTransitions.Add(GetTransitionKey(transition));
            }
            else
            {
                break; // Cannot generate more without conflicts
            }
        }

        return transitions;
    }

    private static void EnsureConnectivity(
        List<State> states,
        List<char> alphabet,
        List<Transition> transitions,
        HashSet<string> addedTransitions,
        Random random)
    {
        for (int i = 0; i < states.Count - 1; i++)
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

            var key = GetTransitionKey(transition);
            if (!addedTransitions.Contains(key))
            {
                transitions.Add(transition);
                addedTransitions.Add(key);
            }
        }
    }

    private static void EnsureAllSymbolsPresent(
        AutomatonType type,
        List<State> states,
        List<char> alphabet,
        List<Transition> transitions,
        HashSet<string> addedTransitions,
        Random random)
    {
        var present = transitions.Select(t => t.Symbol).Where(c => c != '\0').ToHashSet();
        foreach (var symbol in alphabet)
        {
            if (present.Contains(symbol)) continue;

            // Try to add a transition using this symbol
            const int maxAttempts = 100;
            for (int attempt = 0; attempt < maxAttempts; attempt++)
            {
                var fromState = states[random.Next(states.Count)].Id;
                var toState = states[random.Next(states.Count)].Id;
                var candidate = new Transition { FromStateId = fromState, ToStateId = toState, Symbol = symbol };
                var key = GetTransitionKey(candidate);
                if (addedTransitions.Contains(key)) continue;

                if (type == AutomatonType.DFA)
                {
                    // Cannot have another transition from same state on same symbol
                    if (transitions.Any(t => t.FromStateId == fromState && t.Symbol == symbol))
                        continue;
                }

                transitions.Add(candidate);
                addedTransitions.Add(key);
                break; // placed
            }
        }
    }

    private static Transition? GenerateRandomTransition(
        AutomatonType type,
        List<State> states,
        List<char> alphabet,
        HashSet<string> addedTransitions,
        Random random)
    {
        int maxAttempts = 100; 
        
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

        return null; // Could not generate a valid transition
    }

    private static string GetTransitionKey(Transition transition)
    {
        return $"{transition.FromStateId}-{transition.Symbol}-{transition.ToStateId}";
    }

    #endregion
}
