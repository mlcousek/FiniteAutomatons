using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace FiniteAutomatons.Controllers;

public class HomeController(ILogger<HomeController> logger) : Controller
{
    private readonly ILogger<HomeController> _logger = logger;

    public IActionResult Index()
    {
        _logger.LogInformation("Index action called");

        // Check if there's a custom automaton in TempData
        if (TempData["CustomAutomaton"] != null)
        {
            var modelJson = TempData["CustomAutomaton"] as string;
            _logger.LogInformation("Found CustomAutomaton in TempData, length: {Length}", modelJson?.Length ?? 0);

            if (!string.IsNullOrEmpty(modelJson))
            {
                try
                {
                    var customModel = System.Text.Json.JsonSerializer.Deserialize<AutomatonViewModel>(modelJson);
                    if (customModel != null)
                    {
                        _logger.LogInformation("Successfully deserialized custom automaton: Type={Type}, States={StateCount}, IsCustom={IsCustom}",
                            customModel.Type, customModel.States?.Count ?? 0, customModel.IsCustomAutomaton);

                        // Ensure the flag is set to true for custom automatons
                        customModel.IsCustomAutomaton = true;
                        // Ensure collections are properly initialized
                        customModel.States ??= [];
                        customModel.Transitions ??= [];
                        customModel.Alphabet ??= [];
                        customModel.Input ??= "";
                        return View(customModel);
                    }
                }
                catch (System.Text.Json.JsonException ex)
                {
                    _logger.LogError(ex, "Failed to deserialize custom automaton from TempData");
                    TempData["ErrorMessage"] = "Failed to load custom automaton.";
                }
            }
        }
        else
        {
            _logger.LogInformation("No CustomAutomaton found in TempData, using default");
        }

        // Default DFA: 5 states, alphabet {a, b, c}
        var states = new List<State>
        {
            new() { Id = 1, IsStart = true, IsAccepting = false },
            new() { Id = 2, IsStart = false, IsAccepting = false },
            new() { Id = 3, IsStart = false, IsAccepting = false },
            new() { Id = 4, IsStart = false, IsAccepting = false },
            new() { Id = 5, IsStart = false, IsAccepting = true }
        };
        var transitions = new List<Transition>
        {
            new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
            new() { FromStateId = 1, ToStateId = 3, Symbol = 'b' },
            new() { FromStateId = 1, ToStateId = 4, Symbol = 'c' },

            new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' },
            new() { FromStateId = 2, ToStateId = 5, Symbol = 'b' },
            new() { FromStateId = 2, ToStateId = 3, Symbol = 'c' },

            new() { FromStateId = 3, ToStateId = 4, Symbol = 'a' },
            new() { FromStateId = 3, ToStateId = 3, Symbol = 'b' },
            new() { FromStateId = 3, ToStateId = 1, Symbol = 'c' },

            new() { FromStateId = 4, ToStateId = 5, Symbol = 'a' },
            new() { FromStateId = 4, ToStateId = 2, Symbol = 'b' },
            new() { FromStateId = 4, ToStateId = 4, Symbol = 'c' },

            new() { FromStateId = 5, ToStateId = 5, Symbol = 'a' },
            new() { FromStateId = 5, ToStateId = 5, Symbol = 'b' },
            new() { FromStateId = 5, ToStateId = 5, Symbol = 'c' }
        };
        var alphabet = new List<char> { 'a', 'b', 'c' };
        var defaultModel = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = states,
            Transitions = transitions,
            Alphabet = alphabet,
            IsCustomAutomaton = false // Explicitly set to false for default automaton
        };

        _logger.LogInformation("Returning default automaton model");
        return View(defaultModel);
    }

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
            _logger.LogInformation("CreateAutomaton POST called with Type: {Type}, States: {StateCount}, Transitions: {TransitionCount}",
                model.Type, model.States?.Count ?? 0, model.Transitions?.Count ?? 0);

            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];

            // Log transition symbols for debugging
            foreach (var transition in model.Transitions)
            {
                _logger.LogInformation("Transition: {From} -> {To} on '{Symbol}' (char code: {Code})",
                    transition.FromStateId, transition.ToStateId,
                    transition.Symbol == '\0' ? "NULL" : transition.Symbol.ToString(),
                    (int)transition.Symbol);
            }

            // Validate the automaton
            if (!ValidateAutomaton(model))
            {
                _logger.LogWarning("Automaton validation failed");
                return View(model);
            }

            // Mark as custom automaton
            model.IsCustomAutomaton = true;

            // Store the custom automaton in TempData and redirect to simulator
            var modelJson = System.Text.Json.JsonSerializer.Serialize(model);
            TempData["CustomAutomaton"] = modelJson;

            _logger.LogInformation("Successfully created automaton, redirecting to Index");
            return RedirectToAction("Index");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating automaton");
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
            _logger.LogError(ex, "Error changing automaton type");
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
            _logger.LogError(ex, "Error adding state");
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

            // Handle epsilon transitions for Epsilon NFA
            char transitionSymbol;
            if (model.Type == AutomatonType.EpsilonNFA && (symbol == "?" || symbol == "epsilon" || symbol == "eps" || string.IsNullOrEmpty(symbol)))
            {
                transitionSymbol = '\0'; // Epsilon transition
            }
            else if (!string.IsNullOrEmpty(symbol) && symbol.Length == 1)
            {
                transitionSymbol = symbol[0];
            }
            else
            {
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

            return View("CreateAutomaton", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error adding transition");
            ModelState.AddModelError("", "An error occurred while adding the transition.");
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
            _logger.LogError(ex, "Error removing state");
            ModelState.AddModelError("", "An error occurred while removing the state.");
            return View("CreateAutomaton", model);
        }
    }

    [HttpPost]
    [IgnoreAntiforgeryToken] // Allow integration tests to work without antiforgery tokens
    public IActionResult RemoveTransition(AutomatonViewModel model, int fromStateId, int toStateId, char symbol)
    {
        try
        {
            // Ensure collections are initialized
            model.States ??= [];
            model.Transitions ??= [];
            model.Alphabet ??= [];

            model.Transitions.RemoveAll(t => t.FromStateId == fromStateId && t.ToStateId == toStateId && t.Symbol == symbol);

            // Update alphabet - remove symbol if no longer used
            if (symbol != '\0' && !model.Transitions.Any(t => t.Symbol == symbol))
            {
                model.Alphabet.Remove(symbol);
            }

            return View("CreateAutomaton", model);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing transition");
            ModelState.AddModelError("", "An error occurred while removing the transition.");
            return View("CreateAutomaton", model);
        }
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
