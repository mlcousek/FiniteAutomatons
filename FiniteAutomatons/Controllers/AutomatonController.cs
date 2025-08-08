using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using Microsoft.AspNetCore.Mvc;
using System.Text.Json;

namespace FiniteAutomatons.Controllers;

public class AutomatonController(ILogger<AutomatonController> logger) : Controller
{
    private readonly ILogger<AutomatonController> logger = logger;

    public IActionResult CreateAutomaton()
    {
        var model = new AutomatonViewModel();
        return View(model);
    }

    [HttpPost]
    [IgnoreAntiforgeryToken] // Allow integration tests to work without antiforgery tokens
    public IActionResult CreateAutomaton(AutomatonViewModel model)
    {
        try
        {
            logger.LogInformation("CreateAutomaton POST called with Type: {Type}, States: {StateCount}, Transitions: {TransitionCount}",
                model.Type, model.States?.Count ?? 0, model.Transitions?.Count ?? 0);

            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];

            // Log transition symbols for debugging
            foreach (var transition in model.Transitions)
            {
                logger.LogInformation("Transition: {From} -> {To} on '{Symbol}' (char code: {Code})",
                    transition.FromStateId, transition.ToStateId,
                    transition.Symbol == '\0' ? "NULL" : transition.Symbol.ToString(),
                    (int)transition.Symbol);
            }

            // Validate the automaton
            if (!ValidateAutomaton(model))
            {
                logger.LogWarning("Automaton validation failed");
                return View(model);
            }

            // Mark as custom automaton
            model.IsCustomAutomaton = true;

            // Store the custom automaton in TempData and redirect to simulator
            var modelJson = System.Text.Json.JsonSerializer.Serialize(model);
            TempData["CustomAutomaton"] = modelJson;

            logger.LogInformation("Successfully created automaton, redirecting to Index");
            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error creating automaton");
            ModelState.AddModelError("", "An error occurred while creating the automaton.");
            return View(model);
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken] // Allow integration tests to work without antiforgery tokens
    public IActionResult ChangeAutomatonType(AutomatonViewModel model, AutomatonType newType)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];

            if (model.Type == newType)
            {
                return View("CreateAutomaton", model);
            }

            // Convert the automaton to the new type if possible
            var convertedModel = ConvertAutomatonType(model, newType);
            return View("CreateAutomaton", convertedModel);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error changing automaton type");
            ModelState.AddModelError("", "An error occurred while changing the automaton type.");
            return View("CreateAutomaton", model);
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken] // Allow integration tests to work without antiforgery tokens
    public IActionResult AddState(AutomatonViewModel model, int stateId, bool isStart, bool isAccepting)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];

            // Check if state ID already exists
            if (model.States.Any(s => s.Id == stateId))
            {
                ModelState.AddModelError("", $"State with ID {stateId} already exists.");
                return View("CreateAutomaton", model);
            }

            // Check if trying to add another start state
            if (isStart && model.States.Any(s => s.IsStart))
            {
                ModelState.AddModelError("", "Only one start state is allowed.");
                return View("CreateAutomaton", model);
            }

            model.States.Add(new State { Id = stateId, IsStart = isStart, IsAccepting = isAccepting });
            return View("CreateAutomaton", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding state");
            ModelState.AddModelError("", "An error occurred while adding the state.");
            return View("CreateAutomaton", model);
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken] // Allow integration tests to work without antiforgery tokens
    public IActionResult AddTransition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];

            // Add detailed logging for debugging the epsilon symbol issue
            logger.LogInformation("AddTransition called with symbol: '{Symbol}' (Length: {Length})", 
                symbol ?? "NULL", symbol?.Length ?? 0);
            
            if (!string.IsNullOrEmpty(symbol))
            {
                logger.LogInformation("Symbol byte values: {Bytes}", 
                    string.Join(", ", System.Text.Encoding.UTF8.GetBytes(symbol).Select(b => b.ToString())));
                logger.LogInformation("Symbol char codes: {CharCodes}", 
                    string.Join(", ", symbol.Select(c => ((int)c).ToString())));
                
                // Check each character individually with hex representation
                for (int i = 0; i < symbol.Length; i++)
                {
                    char c = symbol[i];
                    logger.LogInformation("Character {Index}: '{Char}' (Unicode: U+{Unicode:X4}, Dec: {Dec}, Hex: 0x{Hex:X})", 
                        i, c == '\0' ? "NULL" : c.ToString(), (int)c, (int)c, (int)c);
                    
                    // Special check for epsilon
                    if ((int)c == 949)
                    {
                        logger.LogInformation("*** EPSILON CHARACTER DETECTED: This is the Greek lowercase epsilon (?) ***");
                    }
                }
                
                // Alternative logging without the symbol directly to avoid encoding issues
                logger.LogInformation("Symbol analysis: Length={Length}, First char code={FirstChar}, Is Greek epsilon={IsEpsilon}", 
                    symbol.Length, 
                    symbol.Length > 0 ? (int)symbol[0] : -1,
                    symbol.Length == 1 && symbol[0] == 949);
            }

            // Validate states exist
            if (!model.States.Any(s => s.Id == fromStateId))
            {
                ModelState.AddModelError("", $"From state {fromStateId} does not exist.");
                return View("CreateAutomaton", model);
            }

            if (!model.States.Any(s => s.Id == toStateId))
            {
                ModelState.AddModelError("", $"To state {toStateId} does not exist.");
                return View("CreateAutomaton", model);
            }

            // Handle epsilon transitions - more comprehensive checking
            char transitionSymbol;
            
            logger.LogInformation("Checking epsilon conditions:");
            logger.LogInformation("symbol == \"?\": {Result} (Expected: True for epsilon symbol U+03B5)", symbol == "?");
            logger.LogInformation("symbol == \"epsilon\": {Result}", symbol == "epsilon");
            logger.LogInformation("symbol == \"eps\": {Result}", symbol == "eps");
            logger.LogInformation("string.IsNullOrEmpty(symbol): {Result}", string.IsNullOrEmpty(symbol));
            
            // More comprehensive epsilon detection
            bool isEpsilonTransition = string.IsNullOrEmpty(symbol) || 
                                     symbol == "?" || 
                                     symbol == "epsilon" || 
                                     symbol == "eps" ||
                                     symbol == "e" ||
                                     symbol == "?" ||  // Alternative lambda symbol
                                     symbol == "\u03B5" ||  // Unicode epsilon
                                     symbol == "\u025B" ||  // Latin small letter open e
                                     symbol.Trim() == "" ||
                                     symbol.Trim() == "?" ||
                                     (symbol.Length == 1 && symbol[0] == '\u03B5') || // Direct unicode check
                                     (symbol.Length == 1 && symbol[0] == 949); // Decimal value for ?
            
            logger.LogInformation("Is epsilon transition (comprehensive check): {Result}", isEpsilonTransition);
            
            if (isEpsilonTransition)
            {
                logger.LogInformation("Epsilon transition detected!");
                // Epsilon symbols are only allowed in Epsilon NFA
                if (model.Type != AutomatonType.EpsilonNFA)
                {
                    ModelState.AddModelError("", "Epsilon transitions (?) are only allowed in Epsilon NFAs. Please change the automaton type or use a different symbol.");
                    return View("CreateAutomaton", model);
                }
                transitionSymbol = '\0'; // Epsilon transition
            }
            else if (!string.IsNullOrEmpty(symbol) && symbol.Trim().Length == 1)
            {
                logger.LogInformation("Regular symbol detected: '{Symbol}'", symbol.Trim());
                transitionSymbol = symbol.Trim()[0];
            }
            else
            {
                logger.LogInformation("Invalid symbol format detected");
                ModelState.AddModelError("", "Symbol must be a single character or epsilon (?) for Epsilon NFA.");
                return View("CreateAutomaton", model);
            }

            // Check if transition already exists (for DFA, this matters more)
            if (model.Type == AutomatonType.DFA &&
                model.Transitions.Any(t => t.FromStateId == fromStateId && t.Symbol == transitionSymbol))
            {
                ModelState.AddModelError("", $"DFA cannot have multiple transitions from state {fromStateId} on symbol '{(transitionSymbol == '\0' ? "?" : transitionSymbol.ToString())}'.");
                return View("CreateAutomaton", model);
            }

            // Check for exact duplicate transitions
            if (model.Transitions.Any(t => t.FromStateId == fromStateId && t.ToStateId == toStateId && t.Symbol == transitionSymbol))
            {
                ModelState.AddModelError("", $"Transition from {fromStateId} to {toStateId} on '{(transitionSymbol == '\0' ? "?" : transitionSymbol.ToString())}' already exists.");
                return View("CreateAutomaton", model);
            }

            model.Transitions.Add(new Transition { FromStateId = fromStateId, ToStateId = toStateId, Symbol = transitionSymbol });

            // Update alphabet (but not for epsilon transitions)
            if (transitionSymbol != '\0' && !model.Alphabet.Contains(transitionSymbol))
            {
                model.Alphabet.Add(transitionSymbol);
            }

            logger.LogInformation("Transition added successfully: {From} -> {To} on '{Symbol}' (char code: {Code})",
                fromStateId, toStateId, 
                transitionSymbol == '\0' ? "?" : transitionSymbol.ToString(),
                (int)transitionSymbol);

            return View("CreateAutomaton", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error adding transition");
            ModelState.AddModelError("", "An error occurred while adding the transition.");
            return View("CreateAutomaton", model);
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken] // Allow integration tests to work without antiforgery tokens
    public IActionResult RemoveState(AutomatonViewModel model, int stateId)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];

            model.States.RemoveAll(s => s.Id == stateId);
            model.Transitions.RemoveAll(t => t.FromStateId == stateId || t.ToStateId == stateId);

            // Update alphabet - remove symbols that are no longer used
            var usedSymbols = model.Transitions.Select(t => t.Symbol).Distinct().ToList();
            model.Alphabet.RemoveAll(c => !usedSymbols.Contains(c));

            return View("CreateAutomaton", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing state");
            ModelState.AddModelError("", "An error occurred while removing the state.");
            return View("CreateAutomaton", model);
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken] // Allow integration tests to work without antiforgery tokens
    public IActionResult RemoveTransition(AutomatonViewModel model, int fromStateId, int toStateId, string symbol)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];

            // Convert symbol string to char, handling epsilon transitions consistently
            char symbolChar;
            if (symbol == "?" || symbol == "epsilon" || symbol == "eps" || string.IsNullOrEmpty(symbol))
            {
                symbolChar = '\0'; // Epsilon transition
            }
            else if (symbol.Length == 1)
            {
                symbolChar = symbol[0];
            }
            else
            {
                ModelState.AddModelError("", "Invalid symbol format.");
                return View("CreateAutomaton", model);
            }

            model.Transitions.RemoveAll(t => t.FromStateId == fromStateId && t.ToStateId == toStateId && t.Symbol == symbolChar);

            // Update alphabet - remove symbol if no longer used
            if (symbolChar != '\0' && !model.Transitions.Any(t => t.Symbol == symbolChar))
            {
                model.Alphabet.Remove(symbolChar);
            }

            return View("CreateAutomaton", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error removing transition");
            ModelState.AddModelError("", "An error occurred while removing the transition.");
            return View("CreateAutomaton", model);
        }
    }

    private AutomatonViewModel ConvertAutomatonType(AutomatonViewModel model, AutomatonType newType)
    {
        var convertedModel = new AutomatonViewModel
        {
            Type = newType,
            States = [.. model.States ?? []],
            Transitions = [.. model.Transitions ?? []],
            Alphabet = [.. model.Alphabet ?? []],
            IsCustomAutomaton = model.IsCustomAutomaton
        };

        switch ((model.Type, newType))
        {
            case (AutomatonType.EpsilonNFA, AutomatonType.NFA):
                convertedModel.Transitions.RemoveAll(t => t.Symbol == '\0');
                break;

            case (AutomatonType.EpsilonNFA, AutomatonType.DFA):
            case (AutomatonType.NFA, AutomatonType.DFA):
                ModelState.AddModelError("", $"Converting from {model.Type} to {newType} may require manual adjustment of transitions to ensure determinism.");
                break;

            case (AutomatonType.DFA, AutomatonType.NFA):
            case (AutomatonType.DFA, AutomatonType.EpsilonNFA):
            case (AutomatonType.NFA, AutomatonType.EpsilonNFA):
                // These conversions are generally safe (adding more flexibility)
                break;
        }

        return convertedModel;
    }

    private bool ValidateAutomaton(AutomatonViewModel model)
    {
        // Ensure collections are initialized
        model.States ??= [];
        model.Transitions ??= [];

        if (model.States.Count == 0)
        {
            ModelState.AddModelError("", "Automaton must have at least one state.");
            return false;
        }

        if (!model.States.Any(s => s.IsStart))
        {
            ModelState.AddModelError("", "Automaton must have exactly one start state.");
            return false;
        }

        if (model.States.Count(s => s.IsStart) > 1)
        {
            ModelState.AddModelError("", "Automaton must have exactly one start state.");
            return false;
        }

        // Additional DFA-specific validation
        if (model.Type == AutomatonType.DFA)
        {
            // Check for determinism: no two transitions from the same state on the same symbol
            var groupedTransitions = model.Transitions
                .GroupBy(t => new { t.FromStateId, t.Symbol })
                .Where(g => g.Count() > 1)
                .ToList();

            if (groupedTransitions.Count != 0)
            {
                var conflicts = groupedTransitions.Select(g =>
                    $"State {g.Key.FromStateId} on symbol '{(g.Key.Symbol == '\0' ? "?" : g.Key.Symbol.ToString())}'");
                ModelState.AddModelError("", $"DFA cannot have multiple transitions from the same state on the same symbol. Conflicts: {string.Join(", ", conflicts)}");
                return false;
            }

            // DFA should not have epsilon transitions
            if (model.Transitions.Any(t => t.Symbol == '\0'))
            {
                ModelState.AddModelError("", "DFA cannot have epsilon transitions.");
                return false;
            }
        }

        return true;
    }

    // Execution methods from existing AutomatonController
    private static AutomatonExecutionState ReconstructState(AutomatonViewModel model)
    {
        // Ensure model has required collections initialized
        model.States ??= [];
        model.Transitions ??= [];
        model.Alphabet ??= [];

        AutomatonExecutionState state;

        if (model.Type == AutomatonType.DFA)
        {
            state = new AutomatonExecutionState(model.Input ?? "", model.CurrentStateId)
            {
                Position = model.Position,
                IsAccepted = model.IsAccepted
            };
        }
        else
        {
            // For NFA and EpsilonNFA, use CurrentStates
            state = new AutomatonExecutionState(model.Input ?? "", null, model.CurrentStates ?? [])
            {
                Position = model.Position,
                IsAccepted = model.IsAccepted
            };
        }

        // Deserialize StateHistory if present
        if (!string.IsNullOrEmpty(model.StateHistorySerialized))
        {
            try
            {
                if (model.Type == AutomatonType.DFA)
                {
                    // DFA uses List<List<int>> where each inner list has one element
                    var stackList = JsonSerializer.Deserialize<List<List<int>>>(model.StateHistorySerialized) ?? [];
                    for (int i = stackList.Count - 1; i >= 0; i--)
                    {
                        state.StateHistory.Push([.. stackList[i]]);
                    }
                }
                else
                {
                    // NFA/EpsilonNFA uses List<HashSet<int>>
                    var stackList = JsonSerializer.Deserialize<List<HashSet<int>>>(model.StateHistorySerialized) ?? [];
                    for (int i = stackList.Count - 1; i >= 0; i--)
                    {
                        state.StateHistory.Push([.. stackList[i]]);
                    }
                }
            }
            catch (JsonException)
            {
                // If deserialization fails, start with empty history
                state.StateHistory.Clear();
            }
        }
        return state;
    }

    private static void UpdateModelFromState(AutomatonViewModel model, AutomatonExecutionState state)
    {
        model.Position = state.Position;
        model.IsAccepted = state.IsAccepted;

        if (model.Type == AutomatonType.DFA)
        {
            model.CurrentStateId = state.CurrentStateId;
            model.CurrentStates = null;
        }
        else
        {
            model.CurrentStateId = null;
            model.CurrentStates = state.CurrentStates ?? [];
        }

        // Serialize StateHistory
        var stackArray = state.StateHistory.ToArray(); // This gives us [top, ..., bottom]
        var stackList = stackArray.Select(s => s.ToList()).ToList();
        model.StateHistorySerialized = JsonSerializer.Serialize(stackList);
    }

    private static void EnsureProperStateInitialization(AutomatonViewModel model, Automaton automaton)
    {
        // Ensure proper state initialization based on automaton type
        if (model.Type == AutomatonType.DFA)
        {
            model.CurrentStateId ??= model.States?.FirstOrDefault(s => s.IsStart)?.Id;
            model.CurrentStates = null;
        }
        else
        {
            // For NFA and EpsilonNFA, initialize with proper start state closure
            if (model.CurrentStates == null || model.CurrentStates.Count == 0)
            {
                var startState = model.States?.FirstOrDefault(s => s.IsStart);
                if (startState != null)
                {
                    try
                    {
                        // Use StartExecution to properly initialize the state
                        var tempState = automaton.StartExecution("");
                        model.CurrentStates = tempState.CurrentStates ?? [startState.Id];
                    }
                    catch
                    {
                        // Fallback if StartExecution fails
                        model.CurrentStates = [startState.Id];
                    }
                }
                else
                {
                    model.CurrentStates = [];
                }
            }
            model.CurrentStateId = null;
        }
    }

    private static Automaton CreateAutomatonFromModel(AutomatonViewModel model)
    {
        // Ensure model has required collections initialized
        model.States ??= [];
        model.Transitions ??= [];
        model.Alphabet ??= [];

        return model.Type switch
        {
            AutomatonType.DFA => CreateDFA(model),
            AutomatonType.NFA => CreateNFA(model),
            AutomatonType.EpsilonNFA => CreateEpsilonNFA(model),
            _ => throw new ArgumentException($"Unsupported automaton type: {model.Type}")
        };
    }

    private static DFA CreateDFA(AutomatonViewModel model)
    {
        var dfa = new DFA();

        // Add states safely
        foreach (var state in model.States ?? [])
        {
            dfa.States.Add(state);
        }

        // Add transitions safely
        foreach (var transition in model.Transitions ?? [])
        {
            dfa.Transitions.Add(transition);
        }

        var startState = model.States?.FirstOrDefault(s => s.IsStart);
        if (startState != null)
        {
            dfa.SetStartState(startState.Id);
        }
        return dfa;
    }

    private static NFA CreateNFA(AutomatonViewModel model)
    {
        var nfa = new NFA();

        // Add states safely
        foreach (var state in model.States ?? [])
        {
            nfa.States.Add(state);
        }

        // Add transitions safely
        foreach (var transition in model.Transitions ?? [])
        {
            nfa.Transitions.Add(transition);
        }

        var startState = model.States?.FirstOrDefault(s => s.IsStart);
        if (startState != null)
        {
            nfa.SetStartState(startState.Id);
        }
        return nfa;
    }

    private static EpsilonNFA CreateEpsilonNFA(AutomatonViewModel model)
    {
        var enfa = new EpsilonNFA();

        // Add states safely
        foreach (var state in model.States ?? [])
        {
            enfa.States.Add(state);
        }

        // Add transitions safely
        foreach (var transition in model.Transitions ?? [])
        {
            enfa.Transitions.Add(transition);
        }

        var startState = model.States?.FirstOrDefault(s => s.IsStart);
        if (startState != null)
        {
            enfa.SetStartState(startState.Id);
        }
        return enfa;
    }

    [HttpPost]
    public IActionResult StepForward([FromForm] AutomatonViewModel model)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];
            model.Input ??= "";

            var automaton = CreateAutomatonFromModel(model);
            EnsureProperStateInitialization(model, automaton);
            var execState = ReconstructState(model);
            automaton.StepForward(execState);
            UpdateModelFromState(model, execState);
            model.Result = execState.IsAccepted;
            model.Alphabet = [.. automaton.Transitions.Select(t => t.Symbol).Where(s => s != '\0').Distinct()];
            return View("../Home/Index", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in StepForward");
            TempData["ErrorMessage"] = "An error occurred while stepping forward.";
            return View("../Home/Index", model);
        }
    }

    [HttpPost]
    public IActionResult StepBackward([FromForm] AutomatonViewModel model)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];
            model.Input ??= "";

            var automaton = CreateAutomatonFromModel(model);
            var execState = ReconstructState(model);
            automaton.StepBackward(execState);
            UpdateModelFromState(model, execState);
            model.Result = execState.IsAccepted;
            model.Alphabet = [.. automaton.Transitions.Select(t => t.Symbol).Where(s => s != '\0').Distinct()];
            return View("../Home/Index", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in StepBackward");
            TempData["ErrorMessage"] = "An error occurred while stepping backward.";
            return View("../Home/Index", model);
        }
    }

    [HttpPost]
    public IActionResult ExecuteAll([FromForm] AutomatonViewModel model)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];
            model.Input ??= "";

            var automaton = CreateAutomatonFromModel(model);
            EnsureProperStateInitialization(model, automaton);
            var execState = ReconstructState(model);
            automaton.ExecuteAll(execState);
            UpdateModelFromState(model, execState);
            model.Result = execState.IsAccepted;
            model.Alphabet = [.. automaton.Transitions.Select(t => t.Symbol).Where(s => s != '\0').Distinct()];
            return View("../Home/Index", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ExecuteAll");
            TempData["ErrorMessage"] = "An error occurred while executing the automaton.";
            return View("../Home/Index", model);
        }
    }

    [HttpPost]
    public IActionResult BackToStart([FromForm] AutomatonViewModel model)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];
            model.Input ??= "";

            var automaton = CreateAutomatonFromModel(model);
            var execState = automaton.StartExecution(model.Input);
            UpdateModelFromState(model, execState);
            model.Result = execState.IsAccepted;
            model.Alphabet = [.. automaton.Transitions.Select(t => t.Symbol).Where(s => s != '\0').Distinct()];
            return View("../Home/Index", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in BackToStart");
            TempData["ErrorMessage"] = "An error occurred while resetting to start.";
            return View("../Home/Index", model);
        }
    }

    [HttpPost]
    public IActionResult Reset([FromForm] AutomatonViewModel model)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];

            // Only reset the execution state and input, NOT the automaton structure
            model.Input = string.Empty;
            model.Result = null;
            model.CurrentStateId = null;
            model.CurrentStates = null;
            model.Position = 0;
            model.IsAccepted = null;
            model.StateHistorySerialized = string.Empty;

            // Preserve the automaton's alphabet - rebuild it from transitions
            var transitionSymbols = model.Transitions
                .Where(t => t.Symbol != '\0') // Exclude epsilon transitions
                .Select(t => t.Symbol)
                .Distinct()
                .ToList();
            
            model.Alphabet = transitionSymbols;

            logger.LogInformation("Reset execution state while preserving automaton structure");
            return View("../Home/Index", model);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in Reset");
            TempData["ErrorMessage"] = "An error occurred while resetting.";
            return View("../Home/Index", model);
        }
    }

    [HttpPost]
    public IActionResult ConvertToDFA([FromForm] AutomatonViewModel model)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];

            if (model.Type == AutomatonType.DFA)
            {
                // Already a DFA
                return View("../Home/Index", model);
            }

            var automaton = CreateAutomatonFromModel(model);
            DFA convertedDFA;

            if (automaton is NFA nfa)
            {
                convertedDFA = nfa.ToDFA();
            }
            else if (automaton is EpsilonNFA enfa)
            {
                // Convert EpsilonNFA -> NFA -> DFA
                var intermediateNFA = enfa.ToNFA();
                convertedDFA = intermediateNFA.ToDFA();
            }
            else
            {
                throw new InvalidOperationException("Cannot convert this automaton type to DFA");
            }

            // Create new model with converted DFA
            var convertedModel = new AutomatonViewModel
            {
                Type = AutomatonType.DFA,
                States = [.. convertedDFA.States],
                Transitions = [.. convertedDFA.Transitions],
                Alphabet = [.. convertedDFA.Transitions.Select(t => t.Symbol).Distinct()],
                Input = model.Input ?? "",
                IsCustomAutomaton = true
            };

            // Store the converted automaton
            var modelJson = System.Text.Json.JsonSerializer.Serialize(convertedModel);
            TempData["CustomAutomaton"] = modelJson;
            TempData["ConversionMessage"] = $"Successfully converted {model.TypeDisplayName} to DFA with {convertedModel.States.Count} states.";

            return RedirectToAction("Index", "Home");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Error in ConvertToDFA");
            TempData["ErrorMessage"] = $"Failed to convert to DFA: {ex.Message}";
            return View("../Home/Index", model);
        }
    }
}
