using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FiniteAutomatons.Services.Services;

public class InputGenerationService(ILogger<InputGenerationService> logger, IAutomatonBuilderService automatonBuilderService) : IInputGenerationService
{
    private const char BottomOfStack = '#';
    private const int MaxPdaAcceptingSearchCandidates = 6000;
    private const int MaxPdaRejectingSearchCandidates = 6000;
    private const int MaxPdaExactEnumerationLength = 6;
    private const int MaxPdaInferenceExploredStates = 12000;
    private const int MaxPdaInferenceRuntimeStackDepth = 64;

    private readonly ILogger<InputGenerationService> logger = logger;
    private readonly IAutomatonBuilderService automatonBuilderService = automatonBuilderService;

    public string GenerateRandomString(AutomatonViewModel automaton, int minLength = 0, int maxLength = 10, int? seed = null)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating random string for automaton type {Type}, length {MinLength}-{MaxLength}",
            automaton.Type, minLength, maxLength);
        }
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
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generated random string: '{String}'", generatedString);
        }
        return generatedString;
    }

    public string? GenerateAcceptingString(AutomatonViewModel automaton, int maxLength = 20)
    {
        LogAcceptingStringGenerationStart(automaton);

        if (IsPdaType(automaton.Type))
        {
            if (!ValidatePdaForAcceptingString(automaton))
                return null;

            return GenerateAcceptingStringForPda(automaton, maxLength);
        }

        var (IsValid, StartState, AcceptingStates) = ValidateAutomatonForAcceptingString(automaton);
        if (!IsValid)
            return null;

        return GenerateAcceptingStringViaBfs(automaton, StartState!, AcceptingStates!, maxLength);
    }

    private bool ValidatePdaForAcceptingString(AutomatonViewModel automaton)
    {
        if (automaton.States == null || automaton.Transitions == null)
        {
            logger.LogWarning("Cannot generate accepting string for PDA - no states or transitions");
            return false;
        }

        var startState = automaton.States.FirstOrDefault(s => s.IsStart);
        if (startState == null)
        {
            logger.LogWarning("Cannot generate accepting string for PDA - no start state");
            return false;
        }

        var requiresAcceptingState = automaton.AcceptanceMode is PDAAcceptanceMode.FinalStateOnly or PDAAcceptanceMode.FinalStateAndEmptyStack;
        if (requiresAcceptingState && !automaton.States.Any(s => s.IsAccepting))
        {
            logger.LogWarning("Cannot generate accepting string for PDA - mode {AcceptanceMode} requires an accepting state",
                automaton.AcceptanceMode);
            return false;
        }

        return true;
    }

    private (bool IsValid, State? StartState, HashSet<int>? AcceptingStates) ValidateAutomatonForAcceptingString(AutomatonViewModel automaton)
    {
        if (automaton.States == null || automaton.Transitions == null)
        {
            logger.LogWarning("Cannot generate accepting string - no states or transitions");
            return (false, null, null);
        }

        var startState = automaton.States.FirstOrDefault(s => s.IsStart);
        if (startState == null)
        {
            logger.LogWarning("Cannot generate accepting string - no start state");
            return (false, null, null);
        }

        var acceptingStates = automaton.States.Where(s => s.IsAccepting).Select(s => s.Id).ToHashSet();
        if (acceptingStates.Count == 0)
        {
            logger.LogWarning("Cannot generate accepting string - no accepting states");
            return (false, null, null);
        }

        return (true, startState, acceptingStates);
    }

    private string? GenerateAcceptingStringForPda(AutomatonViewModel automaton, int maxLength)
    {
        var alphabet = GetOrInferAlphabet(automaton);
        var initialStackBottomFirst = DeserializeInitialStackBottomFirst(automaton.InitialStackSerialized);
        var pda = automaton.Type == AutomatonType.DPDA
            ? (Automaton)automatonBuilderService.CreateDPDA(automaton)
            : automatonBuilderService.CreateNPDA(automaton);

        bool emptyAccepted = TryEmptyStringForPda(pda, initialStackBottomFirst);
        var found = SearchPdaAcceptingString(automaton, pda, alphabet, maxLength, initialStackBottomFirst);
        if (found != null)
            return found;

        if (initialStackBottomFirst == null
            && automaton.States?.Any(s => s.IsAccepting) == true
            && TryInferInitialStackAndInputForPdaAccepting(automaton, pda, maxLength,
                out var inferredStackBottomFirst, out var inferredInput))
        {
            automaton.InitialStackSerialized = JsonSerializer.Serialize(inferredStackBottomFirst);
            logger.LogInformation("Inferred PDA initial stack '{InitialStack}' with accepting input '{Input}'",
                string.Join(',', inferredStackBottomFirst), inferredInput);
            return inferredInput;
        }

        if (emptyAccepted && ShouldAllowEmptyFallbackForPda(automaton, initialStackBottomFirst))
            return string.Empty;

        return null;
    }

    private bool TryEmptyStringForPda(Automaton pda, List<char>? initialStackBottomFirst)
    {
        if (TryEvaluatePdaCandidate(pda, string.Empty, initialStackBottomFirst, out var isAccepted) && isAccepted)
        {
            logger.LogInformation("Found accepting empty string for PDA");
            return true;
        }

        return false;
    }

    private string? SearchPdaAcceptingString(AutomatonViewModel automaton, Automaton pda, List<char> alphabet, int maxLength,
        List<char>? initialStackBottomFirst)
    {
        int checkedCandidates = 0;

        foreach (var candidate in BuildModeAwarePdaAcceptingCandidates(automaton, alphabet, maxLength, initialStackBottomFirst))
        {
            checkedCandidates++;
            if (TryPdaCandidate(pda, candidate, initialStackBottomFirst))
                return candidate;

            if (checkedCandidates >= MaxPdaAcceptingSearchCandidates)
            {
                logger.LogWarning("Stopped PDA accepting search after {Checked} candidates", checkedCandidates);
                return null;
            }
        }

        var random = new Random();
        foreach (var candidate in EnumeratePdaSearchCandidates(alphabet, maxLength, MaxPdaAcceptingSearchCandidates - checkedCandidates, random))
        {
            checkedCandidates++;
            if (TryPdaCandidate(pda, candidate, initialStackBottomFirst))
                return candidate;
        }

        logger.LogWarning("No accepting PDA string found within length {MaxLength} after {Checked} candidates",
            maxLength, checkedCandidates);
        return null;
    }

    private IEnumerable<string> BuildModeAwarePdaAcceptingCandidates(AutomatonViewModel automaton, List<char> alphabet,
        int maxLength, List<char>? initialStackBottomFirst)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        if (automaton.States != null)
        {
            var startState = automaton.States.FirstOrDefault(s => s.IsStart);
            var acceptingStates = automaton.States.Where(s => s.IsAccepting).Select(s => s.Id).ToHashSet();

            if (startState != null && acceptingStates.Count > 0)
            {
                var statePathCandidate = GenerateAcceptingStringViaBfs(automaton, startState, acceptingStates, maxLength);
                if (!string.IsNullOrEmpty(statePathCandidate) && statePathCandidate.Length <= maxLength && seen.Add(statePathCandidate))
                {
                    yield return statePathCandidate;
                }
            }
        }

        if ((automaton.AcceptanceMode is PDAAcceptanceMode.EmptyStackOnly or PDAAcceptanceMode.FinalStateAndEmptyStack)
            && initialStackBottomFirst is { Count: > 1 })
        {
            var stackDrainingCandidate = BuildStackDrainingCandidate(automaton, initialStackBottomFirst, maxLength);
            if (!string.IsNullOrEmpty(stackDrainingCandidate) && seen.Add(stackDrainingCandidate))
            {
                yield return stackDrainingCandidate;
            }
        }

        var popCandidate = automaton.Transitions?
            .FirstOrDefault(t => t.Symbol != '\0' && t.StackPop.HasValue && t.StackPop.Value != '\0')
            ?.Symbol;
        if (popCandidate.HasValue)
        {
            var candidate = popCandidate.Value.ToString();
            if (candidate.Length <= maxLength && seen.Add(candidate))
            {
                yield return candidate;
            }
        }

        foreach (var symbol in alphabet.Take(Math.Min(3, alphabet.Count)))
        {
            var candidate = symbol.ToString();
            if (candidate.Length <= maxLength && seen.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    private bool TryInferInitialStackAndInputForPdaAccepting(AutomatonViewModel automaton, Automaton pda, int maxLength,
        out List<char> inferredStackBottomFirst, out string inferredInput)
    {
        inferredStackBottomFirst = [];
        inferredInput = string.Empty;

        if (automaton.States == null || automaton.Transitions == null)
            return false;

        var startState = automaton.States.FirstOrDefault(s => s.IsStart);
        if (startState == null)
            return false;

        var acceptingStates = automaton.States.Where(s => s.IsAccepting).Select(s => s.Id).ToHashSet();
        var requiresAcceptingState = automaton.AcceptanceMode is PDAAcceptanceMode.FinalStateOnly or PDAAcceptanceMode.FinalStateAndEmptyStack;
        if (requiresAcceptingState && (acceptingStates.Count == 0 || !CanReachAnyState(startState.Id, acceptingStates, automaton.Transitions)))
            return false;

        var queue = new Queue<(int StateId, string Input, List<char> RuntimeStackTopFirst, List<char> RequiredInitialTopFirst)>();
        var visited = new HashSet<string>(StringComparer.Ordinal);
        queue.Enqueue((startState.Id, string.Empty, [], []));
        var exploredStates = 0;

        while (queue.Count > 0)
        {
            exploredStates++;
            if (exploredStates > MaxPdaInferenceExploredStates)
            {
                logger.LogWarning("Stopped PDA initial-stack inference after exploring {Count} states", exploredStates);
                return false;
            }

            var current = queue.Dequeue();
            if (current.Input.Length > maxLength)
                continue;

            string key = $"{current.StateId}|{current.Input.Length}|{new string([.. current.RuntimeStackTopFirst])}|{new string([.. current.RequiredInitialTopFirst])}";
            if (!visited.Add(key))
                continue;

            bool isAcceptingState = automaton.States.Any(s => s.Id == current.StateId && s.IsAccepting);
            bool stackEmptyBeyondBottom = current.RuntimeStackTopFirst.Count == 0;
            bool accepted = automaton.AcceptanceMode switch
            {
                PDAAcceptanceMode.FinalStateOnly => isAcceptingState,
                PDAAcceptanceMode.EmptyStackOnly => stackEmptyBeyondBottom,
                PDAAcceptanceMode.FinalStateAndEmptyStack => isAcceptingState && stackEmptyBeyondBottom,
                _ => isAcceptingState && stackEmptyBeyondBottom
            };

            if (accepted && current.Input.Length > 0 && current.RequiredInitialTopFirst.Count > 0)
            {
                var candidateStack = new List<char> { BottomOfStack };
                candidateStack.AddRange(current.RequiredInitialTopFirst.AsEnumerable().Reverse());

                if (TryEvaluatePdaCandidate(pda, current.Input, candidateStack, out var simulatedAccepted) && simulatedAccepted)
                {
                    inferredStackBottomFirst = candidateStack;
                    inferredInput = current.Input;
                    return true;
                }
            }

            var outgoing = automaton.Transitions.Where(t => t.FromStateId == current.StateId);
            foreach (var transition in outgoing)
            {
                if (transition.Symbol == '\0'
                    && transition.ToStateId == current.StateId
                    && !transition.StackPop.HasValue
                    && string.IsNullOrEmpty(transition.StackPush))
                {
                    continue;
                }

                var runtime = new List<char>(current.RuntimeStackTopFirst);
                var required = new List<char>(current.RequiredInitialTopFirst);

                if (transition.StackPop.HasValue && transition.StackPop.Value != '\0')
                {
                    var pop = transition.StackPop.Value;
                    if (runtime.Count > 0)
                    {
                        if (runtime[0] != pop)
                            continue;
                        runtime.RemoveAt(0);
                    }
                    else
                    {
                        required.Add(pop);
                    }
                }

                if (!string.IsNullOrEmpty(transition.StackPush))
                {
                    for (int i = transition.StackPush.Length - 1; i >= 0; i--)
                    {
                        runtime.Insert(0, transition.StackPush[i]);
                    }
                }

                if (runtime.Count > MaxPdaInferenceRuntimeStackDepth || required.Count > MaxPdaInferenceRuntimeStackDepth)
                    continue;

                var nextInput = transition.Symbol == '\0'
                    ? current.Input
                    : current.Input + transition.Symbol;

                if (nextInput.Length > maxLength)
                    continue;

                queue.Enqueue((transition.ToStateId, nextInput, runtime, required));
            }
        }

        return false;
    }

    private static bool CanReachAnyState(int startStateId, HashSet<int> targetStates, List<Transition> transitions)
    {
        var queue = new Queue<int>();
        var visited = new HashSet<int>();
        queue.Enqueue(startStateId);

        while (queue.Count > 0)
        {
            var current = queue.Dequeue();
            if (!visited.Add(current))
                continue;

            if (targetStates.Contains(current))
                return true;

            foreach (var next in transitions.Where(t => t.FromStateId == current).Select(t => t.ToStateId))
            {
                queue.Enqueue(next);
            }
        }

        return false;
    }

    private static string? BuildStackDrainingCandidate(AutomatonViewModel automaton, List<char> initialStackBottomFirst, int maxLength)
    {
        if (automaton.Transitions == null || initialStackBottomFirst.Count <= 1)
            return null;

        var consumingTransitions = automaton.Transitions.Where(t => t.Symbol != '\0').ToList();
        if (consumingTransitions.Count == 0)
            return null;

        var symbolsToPopTopFirst = initialStackBottomFirst.Skip(1).Reverse();
        var candidate = new List<char>();

        foreach (var stackSymbol in symbolsToPopTopFirst)
        {
            var popTransition = consumingTransitions.FirstOrDefault(t => t.StackPop == stackSymbol);
            if (popTransition == null)
                return null;

            candidate.Add(popTransition.Symbol);
            if (candidate.Count > maxLength)
                return null;
        }

        return candidate.Count > 0 ? new string([.. candidate]) : null;
    }

    private bool TryPdaCandidate(Automaton pda, string candidate, List<char>? initialStackBottomFirst)
    {
        if (!TryEvaluatePdaCandidate(pda, candidate, initialStackBottomFirst, out var isAccepted))
            return false;

        if (isAccepted)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Found accepting PDA string: '{String}'", candidate);
            }
            return true;
        }

        return false;
    }

    private bool TryEvaluatePdaCandidate(Automaton pda, string candidate, List<char>? initialStackBottomFirst, out bool isAccepted)
    {
        try
        {
            var initialStack = BuildInitialStack(initialStackBottomFirst);
            var executionState = pda.StartExecution(candidate, initialStack);
            pda.ExecuteAll(executionState);
            isAccepted = executionState.IsAccepted == true;
            return true;
        }
        catch (Exception ex)
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug(ex, "PDA simulation error for candidate '{Candidate}'", candidate);
            }
            isAccepted = false;
            return false;
        }
    }

    private List<char>? DeserializeInitialStackBottomFirst(string? initialStackSerialized)
    {
        if (string.IsNullOrWhiteSpace(initialStackSerialized))
            return null;

        try
        {
            var raw = JsonSerializer.Deserialize<List<char>>(initialStackSerialized) ?? [];
            if (raw.Count == 0)
                return null;

            return NormalizeInitialStackBottomFirst(raw);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deserialize initial stack for PDA input generation. Falling back to default stack.");
            return null;
        }
    }

    private static Stack<char>? BuildInitialStack(List<char>? initialStackBottomFirst)
    {
        return initialStackBottomFirst is { Count: > 0 }
            ? new Stack<char>(initialStackBottomFirst)
            : null;
    }

    private static List<char> NormalizeInitialStackBottomFirst(List<char> raw)
    {
        var normalized = raw.Where(c => c != '\0' && c != 'ε').ToList();

        if (normalized.Count == 0)
            return [BottomOfStack];

        if (normalized[0] == BottomOfStack)
            return normalized;

        if (normalized[^1] == BottomOfStack)
        {
            normalized.Reverse();
            return normalized;
        }

        normalized.Insert(0, BottomOfStack);
        return normalized;
    }

    private static bool IsPdaType(AutomatonType type)
    {
        return type == AutomatonType.DPDA || type == AutomatonType.NPDA;
    }

    private string? GenerateAcceptingStringViaBfs(AutomatonViewModel automaton, State startState, HashSet<int> acceptingStates, int maxLength)
    {
        var queue = new Queue<(int StateId, string Path)>();
        var visited = new HashSet<(int, int)>();
        string? shortestAcceptingPath = null;

        queue.Enqueue((startState.Id, string.Empty));

        while (queue.Count > 0)
        {
            var (currentState, currentPath) = queue.Dequeue();

            if (currentPath.Length > maxLength)
                continue;

            if (acceptingStates.Contains(currentState))
            {
                if (currentPath.Length > 0)
                {
                    LogFoundAcceptingString(currentPath);
                    return currentPath;
                }
                shortestAcceptingPath ??= currentPath;
            }

            var stateKey = (currentState, currentPath.Length);
            if (!visited.Add(stateKey))
                continue;

            EnqueueNextTransitions(automaton, queue, currentState, currentPath);
        }

        return ReturnAcceptingPathOrNull(shortestAcceptingPath, maxLength);
    }

    private static void EnqueueNextTransitions(AutomatonViewModel automaton, Queue<(int StateId, string Path)> queue, int currentState, string currentPath)
    {
        var transitions = automaton.Transitions!.Where(t => t.FromStateId == currentState);

        foreach (var transition in transitions)
        {
            var symbol = transition.Symbol;
            var nextPath = symbol == '\0' ? currentPath : currentPath + symbol;
            queue.Enqueue((transition.ToStateId, nextPath));
        }
    }

    private string? ReturnAcceptingPathOrNull(string? shortestAcceptingPath, int maxLength)
    {
        if (shortestAcceptingPath != null)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Found accepting string (empty): '{String}'", shortestAcceptingPath);
            }
            return shortestAcceptingPath;
        }

        logger.LogWarning("No accepting string found within length {MaxLength}", maxLength);
        return null;
    }

    private void LogAcceptingStringGenerationStart(AutomatonViewModel automaton)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating accepting string for {Type} with {States} states",
                automaton.Type, automaton.States?.Count ?? 0);
        }
    }

    private void LogFoundAcceptingString(string path)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Found accepting string: '{String}'", path);
        }
    }

    public string? GenerateRandomAcceptingString(AutomatonViewModel automaton, int minLength = 0, int maxLength = 50, int maxAttempts = 100, int? seed = null)
    {
        LogRandomAcceptingGenerationStart(automaton, minLength, maxLength, maxAttempts);

        if (IsPdaType(automaton.Type))
        {
            if (!ValidatePdaForAcceptingString(automaton))
                return null;

            return GenerateRandomAcceptingStringForPda(automaton, minLength, maxLength, maxAttempts, seed);
        }

        var (IsValid, StartState, AcceptingStates) = ValidateAutomatonForRandomAccepting(automaton);
        if (!IsValid)
            return null;

        return PerformRandomWalkAttempts(StartState!, AcceptingStates!,
            automaton.Transitions!, minLength, maxLength, maxAttempts, seed);
    }

    private (bool IsValid, State? StartState, HashSet<int>? AcceptingStates) ValidateAutomatonForRandomAccepting(AutomatonViewModel automaton)
    {
        if (automaton.States == null || automaton.Transitions == null)
        {
            logger.LogWarning("Cannot generate random accepting string - no states or transitions");
            return (false, null, null);
        }

        var startState = automaton.States.FirstOrDefault(s => s.IsStart);
        if (startState == null)
        {
            logger.LogWarning("Cannot generate random accepting string - no start state");
            return (false, null, null);
        }

        var acceptingStates = automaton.States.Where(s => s.IsAccepting).Select(s => s.Id).ToHashSet();
        if (acceptingStates.Count == 0)
        {
            logger.LogWarning("Cannot generate random accepting string - no accepting states");
            return (false, null, null);
        }

        return (true, startState, acceptingStates);
    }

    private string? PerformRandomWalkAttempts(State startState, HashSet<int> acceptingStates,
        List<Transition> transitions, int minLength, int maxLength, int maxAttempts, int? seed)
    {
        var random = seed.HasValue ? new Random(seed.Value) : new Random();
        var transitionsByState = transitions
            .GroupBy(t => t.FromStateId)
            .ToDictionary(g => g.Key, g => g.ToList());

        string? emptyAcceptingFallback = null;

        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var result = TryRandomWalk(startState.Id, acceptingStates, transitionsByState, minLength, maxLength, random);

            if (TryReturnNonEmptyResult(result, attempt, out var nonEmptyResult))
                return nonEmptyResult;

            if (result != null && emptyAcceptingFallback == null)
            {
                emptyAcceptingFallback = result;
                LogFoundEmptyAcceptingString(attempt);
            }
        }

        return ReturnFallbackOrNull(emptyAcceptingFallback, maxAttempts);
    }

    private bool TryReturnNonEmptyResult(string? result, int attempt, out string? nonEmptyResult)
    {
        if (result != null && result.Length > 0)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Found random accepting string on attempt {Attempt}: '{String}'",
                    attempt + 1, result);
            }
            nonEmptyResult = result;
            return true;
        }

        nonEmptyResult = null;
        return false;
    }

    private string? ReturnFallbackOrNull(string? emptyAcceptingFallback, int maxAttempts)
    {
        if (emptyAcceptingFallback != null)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Returning empty accepting string as fallback after {MaxAttempts} attempts",
                    maxAttempts);
            }
            return emptyAcceptingFallback;
        }

        logger.LogWarning("No random accepting string found after {MaxAttempts} attempts", maxAttempts);
        return null;
    }

    private void LogRandomAcceptingGenerationStart(AutomatonViewModel automaton, int minLength, int maxLength, int maxAttempts)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating random accepting string for {Type}, length {MinLength}-{MaxLength}, attempts {MaxAttempts}",
                automaton.Type, minLength, maxLength, maxAttempts);
        }
    }

    private void LogFoundEmptyAcceptingString(int attempt)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Found empty accepting string on attempt {Attempt}, continuing to search for non-empty",
                attempt + 1);
        }
    }

    private string? GenerateRandomAcceptingStringForPda(AutomatonViewModel automaton, int minLength, int maxLength, int maxAttempts, int? seed)
    {
        var alphabet = GetOrInferAlphabet(automaton);
        if (alphabet.Count == 0)
        {
            logger.LogWarning("PDA has no alphabet for random generation");
            return null;
        }

        var initialStackBottomFirst = DeserializeInitialStackBottomFirst(automaton.InitialStackSerialized);
        var pda = automaton.Type == AutomatonType.DPDA
            ? (Automaton)automatonBuilderService.CreateDPDA(automaton)
            : automatonBuilderService.CreateNPDA(automaton);
        var random = seed.HasValue ? new Random(seed.Value) : new Random();

        var emptyStringFallback = minLength == 0 && ShouldAllowEmptyFallbackForPda(automaton, initialStackBottomFirst)
            ? CheckEmptyStringForPda(pda, initialStackBottomFirst)
            : null;

        var guided = SearchPdaAcceptingStringWithParameters(automaton, pda, alphabet, minLength, maxLength, maxAttempts, random, initialStackBottomFirst);
        if (guided != null)
            return guided;

        if (initialStackBottomFirst == null
            && automaton.States?.Any(s => s.IsAccepting) == true
            && TryInferInitialStackAndInputForPdaAccepting(automaton, pda, maxLength,
                out var inferredStackBottomFirst, out var inferredInput))
        {
            automaton.InitialStackSerialized = JsonSerializer.Serialize(inferredStackBottomFirst);
            initialStackBottomFirst = inferredStackBottomFirst;

            if (inferredInput.Length >= minLength && inferredInput.Length <= maxLength)
            {
                logger.LogInformation("Inferred PDA initial stack '{InitialStack}' with random-accepting input '{Input}'",
                    string.Join(',', inferredStackBottomFirst), inferredInput);
                return inferredInput;
            }
        }

        var result = TryRandomPdaCandidates(pda, alphabet, minLength, maxLength, maxAttempts, random, initialStackBottomFirst);

        return result ?? ReturnPdaFallbackOrNull(emptyStringFallback, maxAttempts);
    }

    private string? SearchPdaAcceptingStringWithParameters(AutomatonViewModel automaton, Automaton pda, List<char> alphabet,
        int minLength, int maxLength, int maxAttempts, Random random, List<char>? initialStackBottomFirst)
    {
        int checkedCandidates = 0;

        foreach (var candidate in BuildModeAwarePdaAcceptingCandidates(automaton, alphabet, maxLength, initialStackBottomFirst))
        {
            if (candidate.Length < minLength || candidate.Length > maxLength)
                continue;

            checkedCandidates++;
            if (TryPdaCandidate(pda, candidate, initialStackBottomFirst))
                return candidate;

            if (checkedCandidates >= maxAttempts)
                return null;
        }

        foreach (var candidate in EnumeratePdaSearchCandidates(alphabet, maxLength, Math.Max(0, maxAttempts - checkedCandidates), random))
        {
            if (candidate.Length < minLength || candidate.Length > maxLength)
                continue;

            if (TryPdaCandidate(pda, candidate, initialStackBottomFirst))
                return candidate;
        }

        return null;
    }

    private static List<char> GetOrInferAlphabet(AutomatonViewModel automaton)
    {
        var seen = new HashSet<char>();
        var symbols = new List<char>();

        foreach (var symbol in automaton.Alphabet.Where(c => c != '\0'))
        {
            if (seen.Add(symbol))
            {
                symbols.Add(symbol);
            }
        }

        if (automaton.Transitions != null)
        {
            foreach (var symbol in automaton.Transitions.Where(t => t.Symbol != '\0').Select(t => t.Symbol))
            {
                if (seen.Add(symbol))
                {
                    symbols.Add(symbol);
                }
            }
        }

        return symbols;
    }

    private static bool ShouldAllowEmptyFallbackForPda(AutomatonViewModel automaton, List<char>? initialStackBottomFirst)
    {
        if (automaton.AcceptanceMode == PDAAcceptanceMode.EmptyStackOnly
            && (initialStackBottomFirst == null || initialStackBottomFirst.Count <= 1))
        {
            return false;
        }

        return true;
    }

    private string? CheckEmptyStringForPda(Automaton pda, List<char>? initialStackBottomFirst)
    {
        if (TryEvaluatePdaCandidate(pda, string.Empty, initialStackBottomFirst, out var isAccepted) && isAccepted)
        {
            logger.LogInformation("Empty string is accepting for PDA");
            return string.Empty;
        }

        return null;
    }

    private string? TryRandomPdaCandidates(Automaton pda, List<char> alphabet,
        int minLength, int maxLength, int maxAttempts, Random random, List<char>? initialStackBottomFirst)
    {
        for (int attempt = 0; attempt < maxAttempts; attempt++)
        {
            var candidate = GenerateRandomStringFromAlphabet(alphabet, minLength, maxLength, random);

            if (TryPdaCandidateExecution(pda, candidate, attempt, initialStackBottomFirst))
                return candidate;
        }

        return null;
    }

    private static string GenerateRandomStringFromAlphabet(List<char> alphabet, int minLength, int maxLength, Random random)
    {
        var length = random.Next(minLength, maxLength + 1);
        var chars = new char[length];

        for (int i = 0; i < length; i++)
        {
            chars[i] = alphabet[random.Next(alphabet.Count)];
        }

        return new string(chars);
    }

    private bool TryPdaCandidateExecution(Automaton pda, string candidate, int attempt, List<char>? initialStackBottomFirst)
    {
        if (!TryEvaluatePdaCandidate(pda, candidate, initialStackBottomFirst, out var isAccepted))
            return false;

        if (isAccepted && candidate.Length > 0)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Found random accepting PDA string on attempt {Attempt}: '{String}'",
                    attempt + 1, candidate);
            }
            return true;
        }

        return false;
    }

    private string? ReturnPdaFallbackOrNull(string? emptyStringFallback, int maxAttempts)
    {
        if (emptyStringFallback != null)
        {
            logger.LogInformation("Returning empty accepting string for PDA as fallback");
            return emptyStringFallback;
        }

        logger.LogWarning("No random accepting PDA string found after {MaxAttempts} attempts", maxAttempts);
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

            if (acceptingStates.Contains(currentState) && path.Count >= minLength)
            {
                if (path.Count > 0)
                {
                    return new string([.. path]);
                }

                if (!transitionsByState.TryGetValue(currentState, out var availableTransitions) || availableTransitions.Count == 0)
                {
                    return new string([.. path]);
                }
            }

            if (path.Count >= maxLength)
            {
                return null;
            }

            if (!transitionsByState.TryGetValue(currentState, out var transitions) || transitions.Count == 0)
            {
                return null;
            }

            var selectedTransition = transitions[random.Next(transitions.Count)];

            if (selectedTransition.Symbol != '\0')
            {
                path.Add(selectedTransition.Symbol);
            }

            currentState = selectedTransition.ToStateId;
        }

        return null;
    }

    public string? GenerateRejectingString(AutomatonViewModel automaton, int maxLength = 20)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating rejecting string for {Type}", automaton.Type);
        }

        if (!ValidateAutomatonForRejectingString(automaton, out var alphabet))
            return null;

        if (automaton.Type == AutomatonType.DPDA || automaton.Type == AutomatonType.NPDA)
        {
            return GenerateRejectingStringForPda(automaton, alphabet!, maxLength);
        }

        var result = SearchForRejectingString(automaton, alphabet!, maxLength);
        if (result != null)
            return result;

        return GenerateFallbackRejectingString(automaton, alphabet!, maxLength);
    }

    private bool ValidateAutomatonForRejectingString(AutomatonViewModel automaton, out List<char>? alphabet)
    {
        alphabet = null;

        if (automaton.States == null || automaton.Transitions == null || automaton.Alphabet == null)
        {
            logger.LogWarning("Cannot generate rejecting string - incomplete automaton");
            return false;
        }

        alphabet = [.. automaton.Alphabet.Where(c => c != '\0')];
        return alphabet.Count > 0;
    }

    private string? SearchForRejectingString(AutomatonViewModel automaton, List<char> alphabet, int maxLength)
    {
        for (int len = 1; len <= maxLength; len++)
        {
            var testString = GenerateStringOfLength(alphabet, len, 0);

            if (WouldLikelyReject(automaton, testString))
            {
                if (logger.IsEnabled(LogLevel.Information))
                {
                    logger.LogInformation("Found likely rejecting string: '{String}'", testString);
                }
                return testString;
            }
        }

        return null;
    }

    private string? GenerateRejectingStringForPda(AutomatonViewModel automaton, List<char> alphabet, int maxLength)
    {
        logger.LogInformation("Generating rejecting string for PDA");

        var initialStackBottomFirst = DeserializeInitialStackBottomFirst(automaton.InitialStackSerialized);
        var pda = automaton.Type == AutomatonType.DPDA
            ? (Automaton)automatonBuilderService.CreateDPDA(automaton)
            : automatonBuilderService.CreateNPDA(automaton);

        var result = SearchPdaRejectingString(automaton, pda, alphabet, maxLength, initialStackBottomFirst);
        if (result != null)
            return result;

        return GenerateFallbackPdaRejectingString(pda, alphabet, maxLength, initialStackBottomFirst);
    }

    private string? SearchPdaRejectingString(AutomatonViewModel automaton, Automaton pda, List<char> alphabet, int maxLength,
        List<char>? initialStackBottomFirst)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        int checkedCandidates = 0;

        foreach (var candidate in BuildModeAwarePdaRejectingCandidates(automaton, alphabet, maxLength, initialStackBottomFirst))
        {
            checkedCandidates++;
            if (seen.Add(candidate) && TryRejectingCandidate(pda, candidate, initialStackBottomFirst))
                return candidate;

            if (checkedCandidates >= MaxPdaRejectingSearchCandidates)
            {
                logger.LogWarning("Stopped PDA rejecting search after {Checked} candidates", checkedCandidates);
                return null;
            }
        }

        var strategies = new List<Func<string>>
        {
            () => TryUnbalancedParenthesesPattern(alphabet, maxLength),
            () => TryWrongOrderPattern(alphabet, maxLength),
            () => TryExcessiveSymbolPattern(alphabet, maxLength),
            () => TryMismatchedSymbolPattern(alphabet, maxLength)
        };

        foreach (var strategy in strategies)
        {
            var candidate = strategy();
            checkedCandidates++;
            if (candidate.Length > 0 && seen.Add(candidate) && TryRejectingCandidate(pda, candidate, initialStackBottomFirst))
                return candidate;

            if (checkedCandidates >= MaxPdaRejectingSearchCandidates)
            {
                logger.LogWarning("Stopped PDA rejecting search after {Checked} candidates", checkedCandidates);
                return null;
            }
        }

        var random = new Random();
        foreach (var candidate in EnumeratePdaSearchCandidates(alphabet, Math.Min(maxLength, 12),
            MaxPdaRejectingSearchCandidates - checkedCandidates, random))
        {
            checkedCandidates++;
            if (seen.Add(candidate) && TryRejectingCandidate(pda, candidate, initialStackBottomFirst))
                return candidate;
        }

        logger.LogWarning("No rejecting PDA string found within length {MaxLength} after {Checked} candidates",
            maxLength, checkedCandidates);
        return null;
    }

    private IEnumerable<string> BuildModeAwarePdaRejectingCandidates(AutomatonViewModel automaton, List<char> alphabet,
        int maxLength, List<char>? initialStackBottomFirst)
    {
        if (alphabet.Count == 0 || maxLength <= 0)
            yield break;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var consumingTransitions = automaton.Transitions?.Where(t => t.Symbol != '\0').ToList() ?? [];

        if (automaton.AcceptanceMode is PDAAcceptanceMode.EmptyStackOnly or PDAAcceptanceMode.FinalStateAndEmptyStack)
        {
            var pushTransition = consumingTransitions.FirstOrDefault(t => !string.IsNullOrEmpty(t.StackPush));
            if (pushTransition != null)
            {
                var candidate = pushTransition.Symbol.ToString();
                if (candidate.Length <= maxLength && seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        if (automaton.AcceptanceMode is PDAAcceptanceMode.FinalStateOnly or PDAAcceptanceMode.FinalStateAndEmptyStack)
        {
            var nonAcceptingStates = automaton.States?.Where(s => !s.IsAccepting).Select(s => s.Id).ToHashSet() ?? [];
            var nonAcceptingTransition = consumingTransitions.FirstOrDefault(t => nonAcceptingStates.Contains(t.ToStateId));
            if (nonAcceptingTransition != null)
            {
                var candidate = nonAcceptingTransition.Symbol.ToString();
                if (candidate.Length <= maxLength && seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        if (initialStackBottomFirst is { Count: > 1 })
        {
            var top = initialStackBottomFirst[^1];
            var mismatchPop = consumingTransitions
                .FirstOrDefault(t => t.StackPop.HasValue && t.StackPop.Value != '\0' && t.StackPop.Value != top);

            if (mismatchPop != null)
            {
                var candidate = mismatchPop.Symbol.ToString();
                if (candidate.Length <= maxLength && seen.Add(candidate))
                {
                    yield return candidate;
                }
            }
        }

        foreach (var symbol in alphabet.Take(Math.Min(3, alphabet.Count)))
        {
            var candidate = symbol.ToString();
            if (candidate.Length <= maxLength && seen.Add(candidate))
            {
                yield return candidate;
            }
        }
    }

    private static string TryUnbalancedParenthesesPattern(List<char> alphabet, int maxLength)
    {
        if (alphabet.Count < 2) return alphabet[0].ToString();
        var length = Math.Min(5, maxLength);
        return new string(alphabet[0], length);
    }

    private static string TryWrongOrderPattern(List<char> alphabet, int maxLength)
    {
        if (alphabet.Count < 2) return alphabet[0].ToString();
        var length = Math.Min(4, maxLength / 2);
        return new string(alphabet[1], length) + new string(alphabet[0], length);
    }

    private static string TryExcessiveSymbolPattern(List<char> alphabet, int maxLength)
    {
        if (alphabet.Count < 2) return new string(alphabet[0], maxLength);
        var half = Math.Min(3, maxLength / 2);
        return new string(alphabet[0], half) + new string(alphabet[1], half + 2);
    }

    private static string TryMismatchedSymbolPattern(List<char> alphabet, int maxLength)
    {
        if (alphabet.Count < 2) return alphabet[0].ToString();
        var result = string.Empty;
        var count = Math.Min(3, maxLength / alphabet.Count);
        for (int i = 0; i < count; i++)
        {
            result += alphabet[i % alphabet.Count];
        }
        return result + alphabet[0];
    }

    private bool TryRejectingCandidate(Automaton pda, string candidate, List<char>? initialStackBottomFirst)
    {
        if (!TryEvaluatePdaCandidate(pda, candidate, initialStackBottomFirst, out var isAccepted))
        {
            if (logger.IsEnabled(LogLevel.Debug))
            {
                logger.LogDebug("PDA execution failed for rejecting candidate '{Candidate}' - treating as rejecting", candidate);
            }
            return true;
        }

        if (!isAccepted)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Found rejecting PDA string: '{String}'", candidate);
            }
            return true;
        }

        return false;
    }

    private string? GenerateFallbackPdaRejectingString(Automaton pda, List<char> alphabet, int maxLength, List<char>? initialStackBottomFirst)
    {
        var random = new Random();
        foreach (var candidate in EnumeratePdaSearchCandidates(alphabet, Math.Min(maxLength, 12), 1200, random))
        {
            if (TryRejectingCandidate(pda, candidate, initialStackBottomFirst))
                return candidate;
        }

        logger.LogWarning("No rejecting PDA string found within length {MaxLength}", maxLength);
        return null;
    }

    private string? GenerateFallbackRejectingString(AutomatonViewModel automaton, List<char> alphabet, int maxLength)
    {
        var leastUsedSymbol = alphabet
            .OrderBy(c => automaton.Transitions!.Count(t => t.Symbol == c))
            .FirstOrDefault();

        if (leastUsedSymbol == default(char))
        {
            logger.LogWarning("Could not generate rejecting string");
            return null;
        }

        var result = new string(leastUsedSymbol, Math.Min(3, maxLength));
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generated rejecting string using least-used symbol: '{String}'", result);
        }
        return result;
    }

    public List<(string Input, string Description)> GenerateInterestingCases(AutomatonViewModel automaton, int maxLength = 15)
    {
        LogInterestingCasesStart(automaton);

        var cases = new List<(string, string)> { (string.Empty, "Empty string (ε)") };

        if (!ValidateAlphabetForInterestingCases(automaton, out var alphabet))
            return cases;

        AddBasicTestCases(cases, alphabet!, maxLength);
        AddAutomatonSpecificCases(automaton, cases, maxLength);
        AddNfaSpecificCases(automaton, cases, maxLength);
        AddLongStringTest(automaton, cases, maxLength);
        AddPdaSpecificCases(automaton, cases, maxLength);

        LogInterestingCasesComplete(cases.Count);
        return cases;
    }

    private static bool ValidateAlphabetForInterestingCases(AutomatonViewModel automaton, out List<char>? alphabet)
    {
        alphabet = null;

        if (automaton.Alphabet == null || automaton.Alphabet.Count == 0)
            return false;

        alphabet = [.. automaton.Alphabet.Where(c => c != '\0')];
        return alphabet.Count > 0;
    }

    private static void AddBasicTestCases(List<(string, string)> cases, List<char> alphabet, int maxLength)
    {
        cases.Add((alphabet[0].ToString(), "Single character"));

        if (alphabet.Count <= maxLength)
        {
            cases.Add((new string([.. alphabet]), "All alphabet symbols"));
        }

        var repeatedChar = new string(alphabet[0], Math.Min(5, maxLength));
        cases.Add((repeatedChar, $"Repeated '{alphabet[0]}'"));

        if (alphabet.Count >= 2)
        {
            var alternating = string.Concat(Enumerable.Range(0, Math.Min(6, maxLength))
                .Select(i => alphabet[i % 2]));
            cases.Add((alternating, "Alternating pattern"));
        }
    }

    private void AddAutomatonSpecificCases(AutomatonViewModel automaton, List<(string, string)> cases, int maxLength)
    {
        var accepting = GenerateAcceptingString(automaton, maxLength);
        if (accepting != null)
        {
            cases.Add((accepting, "Known accepting string"));
        }

        var rejecting = GenerateRejectingString(automaton, maxLength);
        if (rejecting != null)
        {
            cases.Add((rejecting, "Likely rejecting string"));
        }
    }

    private void AddNfaSpecificCases(AutomatonViewModel automaton, List<(string, string)> cases, int maxLength)
    {
        if (automaton.Type == AutomatonType.NFA || automaton.Type == AutomatonType.EpsilonNFA)
        {
            var nondetCase = GenerateNondeterministicCase(automaton, maxLength);
            if (nondetCase != null)
            {
                cases.Add((nondetCase, "Tests nondeterminism"));
            }
        }

        if (automaton.Type == AutomatonType.EpsilonNFA)
        {
            var epsilonCase = GenerateEpsilonCase(automaton, maxLength);
            if (epsilonCase != null)
            {
                cases.Add((epsilonCase, "Tests ε-transitions"));
            }
        }
    }

    private void AddLongStringTest(AutomatonViewModel automaton, List<(string, string)> cases, int maxLength)
    {
        if (maxLength >= 10)
        {
            var longString = GenerateRandomString(automaton, maxLength - 2, maxLength, null);
            cases.Add((longString, "Long string test"));
        }
    }

    private void AddPdaSpecificCases(AutomatonViewModel automaton, List<(string, string)> cases, int maxLength)
    {
        if (automaton.Type != AutomatonType.DPDA && automaton.Type != AutomatonType.NPDA)
            return;

        logger.LogInformation("Adding PDA-specific interesting cases");

        var pushTransitions = automaton.Transitions?.Where(t => !string.IsNullOrEmpty(t.StackPush)).ToList() ?? [];
        var popTransitions = automaton.Transitions?.Where(t => t.StackPop.HasValue && t.StackPop.Value != '\0').ToList() ?? [];

        AddPdaPushCase(automaton, cases, pushTransitions, maxLength);
        AddPdaPopCase(automaton, cases, popTransitions, maxLength);
        AddPdaPushPopPattern(automaton, cases, pushTransitions, popTransitions, maxLength);
    }

    private static void AddPdaPushCase(AutomatonViewModel automaton, List<(string, string)> cases,
        List<Transition> pushTransitions, int maxLength)
    {
        if (pushTransitions.Count == 0)
            return;

        var t = pushTransitions.First();
        var pathToState = FindPathToState(automaton, t.FromStateId, maxLength);
        var pushSymbol = t.Symbol != '\0' ? t.Symbol.ToString() : string.Empty;
        var repeatCount = Math.Min(4, Math.Max(1, maxLength / Math.Max(1, pushSymbol.Length)));
        var pushSegment = (pathToState ?? string.Empty) + string.Concat(Enumerable.Repeat(pushSymbol, repeatCount));

        if (pushSegment.Length <= maxLength)
        {
            cases.Add((pushSegment, "PDA push-loop (exercises stack growth)"));
        }
    }

    private static void AddPdaPopCase(AutomatonViewModel automaton, List<(string, string)> cases,
        List<Transition> popTransitions, int maxLength)
    {
        if (popTransitions.Count == 0)
            return;

        var t = popTransitions.First();
        var pathToState = FindPathToState(automaton, t.FromStateId, maxLength);
        var symbol = t.Symbol != '\0' ? t.Symbol.ToString() : string.Empty;
        var candidate = (pathToState ?? string.Empty) + symbol;

        if (!string.IsNullOrEmpty(candidate) && candidate.Length <= maxLength)
        {
            cases.Add((candidate, "PDA pop (requires specific stack top)"));
        }
    }

    private static void AddPdaPushPopPattern(AutomatonViewModel automaton, List<(string, string)> cases,
        List<Transition> pushTransitions, List<Transition> popTransitions, int maxLength)
    {
        if (pushTransitions.Count == 0 || popTransitions.Count == 0)
            return;

        var push = pushTransitions.First();
        var pop = popTransitions.First();
        var toPush = FindPathToState(automaton, push.FromStateId, maxLength);
        var toPop = FindPathToState(automaton, pop.FromStateId, maxLength);

        if (toPush == null || toPop == null)
            return;

        var pushSym = push.Symbol != '\0' ? push.Symbol.ToString() : string.Empty;
        var popSym = pop.Symbol != '\0' ? pop.Symbol.ToString() : string.Empty;
        var n = Math.Min(3, Math.Max(1, maxLength / Math.Max(1, pushSym.Length + popSym.Length + 1)));
        var middle = string.Concat(Enumerable.Repeat(pushSym, n));
        var trailing = string.Concat(Enumerable.Repeat(popSym, n));
        var candidate = (toPush ?? string.Empty) + middle + (toPop ?? string.Empty) + trailing;

        if (candidate.Length <= maxLength && candidate.Length > 0)
        {
            cases.Add((candidate, "PDA push/pop pattern (balanced-like)"));
        }
    }

    private void LogInterestingCasesStart(AutomatonViewModel automaton)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generating interesting test cases for {Type}", automaton.Type);
        }
    }

    private void LogInterestingCasesComplete(int count)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generated {Count} interesting test cases", count);
        }
    }

    public string? GenerateNondeterministicCase(AutomatonViewModel automaton, int maxLength = 15)
    {
        logger.LogInformation("Generating nondeterministic test case");

        if (automaton.Transitions == null)
            return null;

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

        var pathToState = FindPathToState(automaton, fromState, maxLength - 1);
        if (pathToState != null)
        {
            var result = pathToState + symbol;
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Generated nondeterministic case: '{String}'", result);
            }
            return result;
        }

        logger.LogInformation("Could not generate nondeterministic case");
        return null;
    }

    public string? GenerateEpsilonCase(AutomatonViewModel automaton, int maxLength = 15)
    {
        logger.LogInformation("Generating epsilon transition test case");

        var epsilonTransitions = GetEpsilonTransitions(automaton);
        if (epsilonTransitions == null)
            return null;

        var transitionsByState = automaton.Transitions!.GroupBy(t => t.FromStateId)
            .ToDictionary(g => g.Key, g => g.ToList());

        var result = TryRandomEpsilonWalks(automaton, epsilonTransitions, transitionsByState, maxLength);
        if (result != null)
            return result;

        return TryDeterministicEpsilonFallback(automaton, epsilonTransitions, maxLength);
    }

    private List<Transition>? GetEpsilonTransitions(AutomatonViewModel automaton)
    {
        if (automaton.Transitions == null)
            return null;

        var epsilonTransitions = automaton.Transitions
            .Where(t => t.Symbol == '\0')
            .ToList();

        if (epsilonTransitions.Count == 0)
        {
            logger.LogInformation("No epsilon transitions found");
            return null;
        }

        return epsilonTransitions;
    }

    private string? TryRandomEpsilonWalks(AutomatonViewModel automaton, List<Transition> epsilonTransitions,
        Dictionary<int, List<Transition>> transitionsByState, int maxLength)
    {
        var random = new Random();
        var epsList = epsilonTransitions.OrderBy(_ => random.Next()).ToList();
        const int attemptsPerEpsilon = 30;

        var startStateId = automaton.States?.FirstOrDefault(s => s.IsStart)?.Id ?? -1;

        foreach (var eps in epsList)
        {
            for (int attempt = 0; attempt < attemptsPerEpsilon; attempt++)
            {
                var candidate = TryRandomWalkToState(startStateId, eps.FromStateId, transitionsByState, maxLength, random);
                if (candidate != null)
                {
                    LogEpsilonCaseFound(attempt, candidate, eps.FromStateId);
                    return candidate;
                }
            }
        }

        return null;
    }

    private string? TryDeterministicEpsilonFallback(AutomatonViewModel automaton, List<Transition> epsilonTransitions, int maxLength)
    {
        var firstEps = epsilonTransitions.First();
        var fallback = FindPathToState(automaton, firstEps.FromStateId, maxLength);

        if (fallback != null)
        {
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Falling back to deterministic epsilon case: '{String}'", fallback);
            }
            return fallback;
        }

        logger.LogInformation("Could not generate epsilon case");
        return null;
    }

    private void LogEpsilonCaseFound(int attempt, string candidate, int stateId)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Generated random epsilon case on attempt {Attempt}: '{String}' (reaches state {State})",
                attempt + 1, candidate, stateId);
        }
    }

    private static string? TryRandomWalkToState(int startStateId, int targetStateId, Dictionary<int, List<Transition>> transitionsByState, int maxLength, Random random)
    {
        if (startStateId < 0) return null;
        var currentState = startStateId;
        var path = new List<char>();
        var visited = new HashSet<(int stateId, int pathLength)>();
        var stepsWithoutProgress = 0;
        const int maxStepsWithoutProgress = 40;

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
                return null;
            }

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

    private static IEnumerable<string> EnumeratePdaSearchCandidates(List<char> alphabet, int maxLength, int maxCandidates, Random random)
    {
        if (alphabet.Count == 0 || maxLength <= 0 || maxCandidates <= 0)
            yield break;

        var seen = new HashSet<string>(StringComparer.Ordinal);
        int yielded = 0;
        int exactLengthLimit = Math.Min(maxLength, MaxPdaExactEnumerationLength);

        for (int len = 1; len <= exactLengthLimit && yielded < maxCandidates; len++)
        {
            foreach (var candidate in EnumerateStrings(alphabet, len))
            {
                if (!seen.Add(candidate))
                    continue;

                yield return candidate;
                yielded++;
                if (yielded >= maxCandidates)
                    yield break;
            }
        }

        int randomAttempts = 0;
        int maxRandomAttempts = maxCandidates * 6;

        while (yielded < maxCandidates && randomAttempts < maxRandomAttempts)
        {
            randomAttempts++;
            var candidate = GenerateRandomStringFromAlphabet(alphabet, 1, maxLength, random);
            if (!seen.Add(candidate))
                continue;

            yield return candidate;
            yielded++;
        }
    }

    private static IEnumerable<string> EnumerateStrings(List<char> alphabet, int length)
    {
        if (length == 0)
        {
            yield return string.Empty;
            yield break;
        }

        var indices = new int[length];
        var n = alphabet.Count;
        while (true)
        {
            var chars = new char[length];
            for (int i = 0; i < length; i++) chars[i] = alphabet[indices[i]];
            yield return new string(chars);

            int pos = length - 1;
            while (pos >= 0)
            {
                indices[pos]++;
                if (indices[pos] < n) break;
                indices[pos] = 0;
                pos--;
            }
            if (pos < 0) break;
        }
    }

    private static bool WouldLikelyReject(AutomatonViewModel automaton, string input)
    {
        var symbolCounts = automaton.Transitions!
            .Where(t => t.Symbol != '\0')
            .GroupBy(t => t.Symbol)
            .ToDictionary(g => g.Key, g => g.Count());

        foreach (var ch in input)
        {
            if (!symbolCounts.TryGetValue(ch, out int value) || value < 2)
            {
                return true;
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
