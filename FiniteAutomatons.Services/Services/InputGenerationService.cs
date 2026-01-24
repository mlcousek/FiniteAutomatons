using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FiniteAutomatons.Services.Services;

public class InputGenerationService(ILogger<InputGenerationService> logger) : IInputGenerationService
{
    private readonly ILogger<InputGenerationService> logger = logger;

    public string GenerateRandomString(AutomatonViewModel automaton, int minLength = 0, int maxLength = 10, int? seed = null)
    {
        logger.LogInformation("Generating random string for automaton type {Type}, length {MinLength}-{MaxLength}",
            automaton.Type, minLength, maxLength);

        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var alphabet = automaton.Alphabet?.Where(c => c != '\0').ToList() ?? [];

        if (alphabet.Count == 0)
        {
            logger.LogWarning("No alphabet available for random string generation");
            return string.Empty;
        }

        var length = random.Next(minLength, maxLength + 1);
        var result = new char[length];

        for (int i = 0; i < length; i++)
        {
            result[i] = alphabet[random.Next(alphabet.Count)];
        }

        var generatedString = new string(result);
        logger.LogInformation("Generated random string: '{String}'", generatedString);
        return generatedString;
    }

    public string? GenerateAcceptingString(AutomatonViewModel automaton, int maxLength = 20)
    {
        logger.LogInformation("Generating accepting string for {Type} with {States} states",
            automaton.Type, automaton.States?.Count ?? 0);

        if (automaton.States == null || automaton.Transitions == null)
        {
            logger.LogWarning("Cannot generate accepting string - no states or transitions");
            return null;
        }

        var startState = automaton.States.FirstOrDefault(s => s.IsStart);
        if (startState == null)
        {
            logger.LogWarning("Cannot generate accepting string - no start state");
            return null;
        }

        var acceptingStates = automaton.States.Where(s => s.IsAccepting).Select(s => s.Id).ToHashSet();
        if (acceptingStates.Count == 0)
        {
            logger.LogWarning("Cannot generate accepting string - no accepting states");
            return null;
        }

        // BFS to find shortest non-empty accepting path (prefer non-empty strings)
        var queue = new Queue<(int StateId, string Path)>();
        var visited = new HashSet<(int, int)>(); // (stateId, pathLength)
        string? shortestAcceptingPath = null;

        queue.Enqueue((startState.Id, string.Empty));

        while (queue.Count > 0)
        {
            var (currentState, currentPath) = queue.Dequeue();

            if (currentPath.Length > maxLength)
                continue;

            // Check if we reached an accepting state
            if (acceptingStates.Contains(currentState))
            {
                // Prefer non-empty paths, but accept empty if it's the only option
                if (currentPath.Length > 0)
                {
                    logger.LogInformation("Found accepting string: '{String}'", currentPath);
                    return currentPath;
                }
                else if (shortestAcceptingPath == null)
                {
                    // Store empty string as fallback if no non-empty path is found
                    shortestAcceptingPath = currentPath;
                }
            }

            var stateKey = (currentState, currentPath.Length);
            if (!visited.Add(stateKey))
                continue;

            // Explore transitions
            var transitions = automaton.Transitions.Where(t => t.FromStateId == currentState);

            foreach (var transition in transitions)
            {
                var symbol = transition.Symbol;
                var nextPath = symbol == '\0' ? currentPath : currentPath + symbol;
                queue.Enqueue((transition.ToStateId, nextPath));
            }
        }

        if (shortestAcceptingPath != null)
        {
            logger.LogInformation("Found accepting string (empty): '{String}'", shortestAcceptingPath);
            return shortestAcceptingPath;
        }

        logger.LogWarning("No accepting string found within length {MaxLength}", maxLength);
        return null;
    }

    public string? GenerateRandomAcceptingString(AutomatonViewModel automaton, int minLength = 0, int maxLength = 50, int maxAttempts = 100, int? seed = null)
    {
        logger.LogInformation("Generating random accepting string for {Type}, length {MinLength}-{MaxLength}, attempts {MaxAttempts}",
            automaton.Type, minLength, maxLength, maxAttempts);

        if (automaton.States == null || automaton.Transitions == null)
        {
            logger.LogWarning("Cannot generate random accepting string - no states or transitions");
            return null;
        }

        var startState = automaton.States.FirstOrDefault(s => s.IsStart);
        if (startState == null)
        {
            logger.LogWarning("Cannot generate random accepting string - no start state");
            return null;
        }

        var acceptingStates = automaton.States.Where(s => s.IsAccepting).Select(s => s.Id).ToHashSet();
        if (acceptingStates.Count == 0)
        {
            logger.LogWarning("Cannot generate random accepting string - no accepting states");
            return null;
        }

        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var transitionsByState = automaton.Transitions
            .GroupBy(t => t.FromStateId)
            .ToDictionary(g => g.Key, g => g.ToList());

        string? emptyAcceptingFallback = null;

        // Try multiple attempts to find an accepting string
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var result = TryRandomWalk(startState.Id, acceptingStates, transitionsByState, minLength, maxLength, random);
            if (result != null)
            {
                // Prefer non-empty strings; store empty as fallback
                if (result.Length > 0)
                {
                    logger.LogInformation("Found random accepting string on attempt {Attempt}: '{String}'", attempt + 1, result);
                    return result;
                }
                else if (emptyAcceptingFallback == null)
                {
                    emptyAcceptingFallback = result;
                    logger.LogInformation("Found empty accepting string on attempt {Attempt}, continuing to search for non-empty", attempt + 1);
                }
            }
        }

        // Return empty string as last resort if found
        if (emptyAcceptingFallback != null)
        {
            logger.LogInformation("Returning empty accepting string as fallback after {MaxAttempts} attempts", maxAttempts);
            return emptyAcceptingFallback;
        }

        logger.LogWarning("No random accepting string found after {MaxAttempts} attempts", maxAttempts);
        return null;
    }

    private static string? TryRandomWalk(
        int startStateId,
        HashSet<int> acceptingStates,
        Dictionary<int, List<Transition>> transitionsByState,
        int minLength,
        int maxLength,
        Random random)
    {
        var currentState = startStateId;
        var path = new List<char>();
        var visitedStates = new HashSet<(int stateId, int pathLength)>();
        var stepsWithoutProgress = 0;
        const int maxStepsWithoutProgress = 20;

        while (path.Count <= maxLength)
        {
            var stateKey = (currentState, path.Count);
            if (!visitedStates.Add(stateKey))
            {
                stepsWithoutProgress++;
                if (stepsWithoutProgress > maxStepsWithoutProgress)
                {
                    return null;
                }
            }
            else
            {
                stepsWithoutProgress = 0;
            }

            // Check if we're in an accepting state and within length constraints
            // Prefer non-empty strings: only accept empty if we have no transitions to continue
            if (acceptingStates.Contains(currentState) && path.Count >= minLength)
            {
                // If path is non-empty, return it immediately
                if (path.Count > 0)
                {
                    return new string([.. path]);
                }

                // If path is empty, check if we have transitions to continue
                if (!transitionsByState.TryGetValue(currentState, out var availableTransitions) || availableTransitions.Count == 0)
                {
                    // No way to continue, return empty string
                    return new string([.. path]);
                }
                // Otherwise, continue walking to try to find non-empty path
            }

            // If we've reached maxLength and not in accepting state, this path failed
            if (path.Count >= maxLength)
            {
                return null;
            }

            // Get available transitions from current state
            if (!transitionsByState.TryGetValue(currentState, out var transitions) || transitions.Count == 0)
            {
                return null;
            }

            // Randomly select a transition
            var selectedTransition = transitions[random.Next(transitions.Count)];

            // Add symbol to path (skip epsilon transitions in path)
            if (selectedTransition.Symbol != '\0')
            {
                path.Add(selectedTransition.Symbol);
            }

            // Move to next state
            currentState = selectedTransition.ToStateId;
        }

        return null;
    }

    public string? GenerateRejectingString(AutomatonViewModel automaton, int maxLength = 20)
    {
        logger.LogInformation("Generating rejecting string for {Type}", automaton.Type);

        if (automaton.States == null || automaton.Transitions == null || automaton.Alphabet == null)
        {
            logger.LogWarning("Cannot generate rejecting string - incomplete automaton");
            return null;
        }

        var alphabet = automaton.Alphabet.Where(c => c != '\0').ToList();
        if (alphabet.Count == 0)
        {
            return null;
        }

        // Try progressively longer strings
        for (int len = 1; len <= maxLength; len++)
        {
            // Try different combinations
            var testString = GenerateStringOfLength(alphabet, len, 0);

            // Simple heuristic: if string uses symbols not in heavily-used transitions, likely to reject
            if (WouldLikelyReject(automaton, testString))
            {
                logger.LogInformation("Found likely rejecting string: '{String}'", testString);
                return testString;
            }
        }

        // Fallback: generate a string with a symbol that has few transitions
        var leastUsedSymbol = alphabet
            .OrderBy(c => automaton.Transitions.Count(t => t.Symbol == c))
            .FirstOrDefault();

        if (leastUsedSymbol != default(char))
        {
            var result = new string(leastUsedSymbol, Math.Min(3, maxLength));
            logger.LogInformation("Generated rejecting string using least-used symbol: '{String}'", result);
            return result;
        }

        logger.LogWarning("Could not generate rejecting string");
        return null;
    }

    public List<(string Input, string Description)> GenerateInterestingCases(AutomatonViewModel automaton, int maxLength = 15)
    {
        logger.LogInformation("Generating interesting test cases for {Type}", automaton.Type);

        var cases = new List<(string, string)>
        {
            // Case 1: Empty string
            (string.Empty, "Empty string (ε)")
        };

        if (automaton.Alphabet == null || automaton.Alphabet.Count == 0)
        {
            return cases;
        }

        var alphabet = automaton.Alphabet.Where(c => c != '\0').ToList();
        if (alphabet.Count == 0)
        {
            return cases;
        }

        // Case 2: Single character from alphabet
        cases.Add((alphabet[0].ToString(), "Single character"));

        // Case 3: All alphabet symbols once
        if (alphabet.Count <= maxLength)
        {
            cases.Add((new string([.. alphabet]), "All alphabet symbols"));
        }

        // Case 4: Repeated character
        var repeatedChar = new string(alphabet[0], Math.Min(5, maxLength));
        cases.Add((repeatedChar, $"Repeated '{alphabet[0]}'"));

        // Case 5: Alternating pattern (if 2+ symbols)
        if (alphabet.Count >= 2)
        {
            var alternating = string.Concat(Enumerable.Range(0, Math.Min(6, maxLength))
                .Select(i => alphabet[i % 2]));
            cases.Add((alternating, "Alternating pattern"));
        }

        // Case 6: Accepting string (if found)
        var accepting = GenerateAcceptingString(automaton, maxLength);
        if (accepting != null)
        {
            cases.Add((accepting, "Known accepting string"));
        }

        // Case 7: Likely rejecting string
        var rejecting = GenerateRejectingString(automaton, maxLength);
        if (rejecting != null)
        {
            cases.Add((rejecting, "Likely rejecting string"));
        }

        // Case 8: Nondeterministic case (for NFAs)
        if (automaton.Type == AutomatonType.NFA || automaton.Type == AutomatonType.EpsilonNFA)
        {
            var nondetCase = GenerateNondeterministicCase(automaton, maxLength);
            if (nondetCase != null)
            {
                cases.Add((nondetCase, "Tests nondeterminism"));
            }
        }

        // Case 9: Epsilon case (for ε-NFAs)
        if (automaton.Type == AutomatonType.EpsilonNFA)
        {
            var epsilonCase = GenerateEpsilonCase(automaton, maxLength);
            if (epsilonCase != null)
            {
                cases.Add((epsilonCase, "Tests ε-transitions"));
            }
        }

        // Case 10: Long string
        if (maxLength >= 10)
        {
            var longString = GenerateRandomString(automaton, maxLength - 2, maxLength, null);
            cases.Add((longString, "Long string test"));
        }

        logger.LogInformation("Generated {Count} interesting test cases", cases.Count);
        return cases;
    }

    public string? GenerateNondeterministicCase(AutomatonViewModel automaton, int maxLength = 15)
    {
        logger.LogInformation("Generating nondeterministic test case");

        if (automaton.Transitions == null)
            return null;

        // Find states with multiple transitions on same symbol (nondeterminism)
        var nondeterministicTransitions = automaton.Transitions
            .GroupBy(t => new { t.FromStateId, t.Symbol })
            .Where(g => g.Count() > 1 && g.Key.Symbol != '\0')
            .FirstOrDefault();

        if (nondeterministicTransitions == null)
        {
            logger.LogInformation("No nondeterminism found in automaton");
            return null;
        }

        var symbol = nondeterministicTransitions.Key.Symbol;
        var fromState = nondeterministicTransitions.Key.FromStateId;

        // Build a string that reaches this nondeterministic choice
        var pathToState = FindPathToState(automaton, fromState, maxLength - 1);
        if (pathToState != null)
        {
            var result = pathToState + symbol;
            logger.LogInformation("Generated nondeterministic case: '{String}'", result);
            return result;
        }

        logger.LogInformation("Could not generate nondeterministic case");
        return null;
    }

    public string? GenerateEpsilonCase(AutomatonViewModel automaton, int maxLength = 15)
    {
        logger.LogInformation("Generating epsilon transition test case");

        if (automaton.Transitions == null)
            return null;

        // Find epsilon transitions (targets we want to reach)
        var epsilonTransitions = automaton.Transitions
            .Where(t => t.Symbol == '\0')
            .ToList();

        if (epsilonTransitions.Count == 0)
        {
            logger.LogInformation("No epsilon transitions found");
            return null;
        }

        // Try to generate a random path that reaches the source of an epsilon transition.
        // If that fails, fall back to first-found deterministic path. If that also fails, return null.
        var random = new Random();
        var transitionsByState = automaton.Transitions.GroupBy(t => t.FromStateId).ToDictionary(g => g.Key, g => g.ToList());

        // Shuffle epsilon transitions to randomize which epsilon we attempt first
        var epsList = epsilonTransitions.OrderBy(_ => random.Next()).ToList();

        const int attemptsPerEpsilon = 30;
        foreach (var eps in epsList)
        {
            for (int attempt = 0; attempt < attemptsPerEpsilon; attempt++)
            {
                var candidate = TryRandomWalkToState(automaton.States?.FirstOrDefault(s => s.IsStart)?.Id ?? -1, eps.FromStateId, transitionsByState, maxLength, random);
                if (candidate != null)
                {
                    logger.LogInformation("Generated random epsilon case on attempt {Attempt}: '{String}' (reaches state {State})", attempt + 1, candidate, eps.FromStateId);
                    return candidate;
                }
            }
        }

        // Fallback: return the first deterministic path to any epsilon source
        var firstEps = epsilonTransitions.First();
        var fallback = FindPathToState(automaton, firstEps.FromStateId, maxLength);
        if (fallback != null)
        {
            logger.LogInformation("Falling back to deterministic epsilon case: '{String}'", fallback);
            return fallback;
        }

        logger.LogInformation("Could not generate epsilon case");
        return null;
    }

    private static string? TryRandomWalkToState(int startStateId, int targetStateId, Dictionary<int, List<Transition>> transitionsByState, int maxLength, Random random)
    {
        if (startStateId < 0) return null;
        var currentState = startStateId;
        var path = new List<char>();
        var visited = new HashSet<(int stateId, int pathLength)>();
        var stepsWithoutProgress = 0;
        const int maxStepsWithoutProgress = 40;

        // Try up to maxLength steps (randomly choosing transitions)
        while (path.Count <= maxLength)
        {
            var stateKey = (currentState, path.Count);
            if (!visited.Add(stateKey))
            {
                stepsWithoutProgress++;
                if (stepsWithoutProgress > maxStepsWithoutProgress)
                    return null;
            }
            else
            {
                stepsWithoutProgress = 0;
            }

            if (currentState == targetStateId)
            {
                return new string([.. path]);
            }

            if (!transitionsByState.TryGetValue(currentState, out var transitions) || transitions.Count == 0)
            {
                return null; // stuck
            }

            // Randomly select a transition
            var selected = transitions[random.Next(transitions.Count)];
            if (selected.Symbol != '\0') path.Add(selected.Symbol);
            currentState = selected.ToStateId;
        }

        return null;
    }

    // Helper methods

    private static string GenerateStringOfLength(List<char> alphabet, int length, int seed)
    {
        var random = new Random(seed);
        var result = new char[length];
        for (int i = 0; i < length; i++)
        {
            result[i] = alphabet[random.Next(alphabet.Count)];
        }
        return new string(result);
    }

    private static bool WouldLikelyReject(AutomatonViewModel automaton, string input)
    {
        // Simple heuristic: check if string contains symbols with sparse transitions
        var symbolCounts = automaton.Transitions!
            .Where(t => t.Symbol != '\0')
            .GroupBy(t => t.Symbol)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var ch in input)
        {
            if (!symbolCounts.TryGetValue(ch, out int value) || value < 2)
            {
                return true; // Likely to get stuck
            }
        }

        return false;
    }

    private static string? FindPathToState(AutomatonViewModel automaton, int targetStateId, int maxLength)
    {
        if (automaton.States == null || automaton.Transitions == null)
            return null;

        var startState = automaton.States.FirstOrDefault(s => s.IsStart);
        if (startState == null)
            return null;

        // BFS to find path
        var queue = new Queue<(int StateId, string Path)>();
        var visited = new HashSet<int>();

        queue.Enqueue((startState.Id, string.Empty));

        while (queue.Count > 0)
        {
            var (currentState, currentPath) = queue.Dequeue();

            if (currentPath.Length > maxLength)
                continue;

            if (currentState == targetStateId)
                return currentPath;

            if (!visited.Add(currentState))
                continue;

            var transitions = automaton.Transitions.Where(t => t.FromStateId == currentState);

            foreach (var transition in transitions)
            {
                var symbol = transition.Symbol;
                var nextPath = symbol == '\0' ? currentPath : currentPath + symbol;
                queue.Enqueue((transition.ToStateId, nextPath));
            }
        }

        return null;
    }
}
