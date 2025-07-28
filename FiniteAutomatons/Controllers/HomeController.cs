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
        // Check if there's a custom automaton in TempData
        if (TempData["CustomAutomaton"] != null)
        {
            var modelJson = TempData["CustomAutomaton"] as string;
            var customModel = System.Text.Json.JsonSerializer.Deserialize<DfaViewModel>(modelJson!);
            // Ensure the flag is set to true for custom automatons
            customModel!.IsCustomAutomaton = true;
            return View(customModel);
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
        var defaultModel = new DfaViewModel
        {
            States = states,
            Transitions = transitions,
            Alphabet = alphabet,
            IsCustomAutomaton = false // Explicitly set to false for default automaton
        };
        return View(defaultModel);
    }

    public IActionResult CreateAutomaton()
    {
        var model = new DfaViewModel();
        return View(model);
    }

    [HttpPost]
    public IActionResult CreateAutomaton(DfaViewModel model)
    {
        // Validate the automaton
        if (!ValidateAutomaton(model))
        {
            return View(model);
        }

        // Mark as custom automaton
        model.IsCustomAutomaton = true;
        
        // Store the custom automaton in TempData and redirect to simulator
        var modelJson = System.Text.Json.JsonSerializer.Serialize(model);
        TempData["CustomAutomaton"] = modelJson;
        return RedirectToAction("Index");
    }

    [HttpPost]
    public IActionResult AddState(DfaViewModel model, int stateId, bool isStart, bool isAccepting)
    {
        // Ensure collections are initialized
        model.States ??= new List<State>();
        model.Transitions ??= new List<Transition>();
        model.Alphabet ??= new List<char>();

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

    [HttpPost]
    public IActionResult AddTransition(DfaViewModel model, int fromStateId, int toStateId, char symbol)
    {
        // Ensure collections are initialized
        model.States ??= new List<State>();
        model.Transitions ??= new List<Transition>();
        model.Alphabet ??= new List<char>();

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

        // Check if transition already exists
        if (model.Transitions.Any(t => t.FromStateId == fromStateId && t.ToStateId == toStateId && t.Symbol == symbol))
        {
            ModelState.AddModelError("", $"Transition from {fromStateId} to {toStateId} on '{symbol}' already exists.");
            return View("CreateAutomaton", model);
        }

        model.Transitions.Add(new Transition { FromStateId = fromStateId, ToStateId = toStateId, Symbol = symbol });
        
        // Update alphabet
        if (!model.Alphabet.Contains(symbol))
        {
            model.Alphabet.Add(symbol);
        }

        return View("CreateAutomaton", model);
    }

    [HttpPost]
    public IActionResult RemoveState(DfaViewModel model, int stateId)
    {
        // Ensure collections are initialized
        model.States ??= new List<State>();
        model.Transitions ??= new List<Transition>();
        model.Alphabet ??= new List<char>();

        model.States.RemoveAll(s => s.Id == stateId);
        model.Transitions.RemoveAll(t => t.FromStateId == stateId || t.ToStateId == stateId);
        
        // Update alphabet - remove symbols that are no longer used
        var usedSymbols = model.Transitions.Select(t => t.Symbol).Distinct().ToList();
        model.Alphabet.RemoveAll(c => !usedSymbols.Contains(c));
        
        return View("CreateAutomaton", model);
    }

    [HttpPost]
    public IActionResult RemoveTransition(DfaViewModel model, int fromStateId, int toStateId, char symbol)
    {
        // Ensure collections are initialized
        model.States ??= new List<State>();
        model.Transitions ??= new List<Transition>();
        model.Alphabet ??= new List<char>();

        model.Transitions.RemoveAll(t => t.FromStateId == fromStateId && t.ToStateId == toStateId && t.Symbol == symbol);
        
        // Update alphabet - remove symbol if no longer used
        if (!model.Transitions.Any(t => t.Symbol == symbol))
        {
            model.Alphabet.Remove(symbol);
        }

        return View("CreateAutomaton", model);
    }

    private bool ValidateAutomaton(DfaViewModel model)
    {
        if (model.States == null || model.States.Count == 0)
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

        return true;
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
