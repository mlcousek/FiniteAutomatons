using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;

namespace FiniteAutomatons.Controllers;

public class HomeController(ILogger<HomeController> logger) : Controller
{
    private readonly ILogger<HomeController> logger = logger;

    public IActionResult Index()
    {
        logger.LogInformation("Index action called");

        // Check if there's a custom automaton in TempData
        if (TempData["CustomAutomaton"] != null)
        {
            var modelJson = TempData["CustomAutomaton"] as string;
            logger.LogInformation("Found CustomAutomaton in TempData, length: {Length}", modelJson?.Length ?? 0);

            if (!string.IsNullOrEmpty(modelJson))
            {
                try
                {
                    var customModel = System.Text.Json.JsonSerializer.Deserialize<AutomatonViewModel>(modelJson);
                    if (customModel != null)
                    {
                        logger.LogInformation("Successfully deserialized custom automaton: Type={Type}, States={StateCount}, IsCustom={IsCustom}",
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
                    logger.LogError(ex, "Failed to deserialize custom automaton from TempData");
                    TempData["ErrorMessage"] = "Failed to load custom automaton.";
                }
            }
        }
        else
        {
            logger.LogInformation("No CustomAutomaton found in TempData, using default");
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

        logger.LogInformation("Returning default automaton model");
        return View(defaultModel);
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
