using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using Microsoft.AspNetCore.Mvc;

namespace FiniteAutomatons.Controllers;

public class AutomatonController(ILogger<AutomatonController> logger) : Controller
{
    private readonly ILogger<AutomatonController> _logger = logger;

    [HttpPost]
    public IActionResult SimulateDfa([FromForm] DfaViewModel model)
    {
        var dfa = new DFA();
        dfa.States.AddRange(model.States);
        dfa.Transitions.AddRange(model.Transitions);
        var result = dfa.Execute(model.Input);
        model.Result = result;
        model.Alphabet = [.. dfa.Transitions.Select(t => t.Symbol).Distinct()];
        return View("../Home/Index", model);
    }
}
