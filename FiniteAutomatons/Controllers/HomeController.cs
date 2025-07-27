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
        // Define a more complex DFA: 5 states, alphabet {a, b, c}
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
        var model = new DfaViewModel
        {
            States = states,
            Transitions = transitions,
            Alphabet = alphabet
        };
        return View(model);
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
