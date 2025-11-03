using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Utilities;
using Microsoft.AspNetCore.Mvc.Filters;

namespace FiniteAutomatons.Filters;

public class AutomatonModelFilter : IActionFilter
{
    private static readonly HashSet<string> EpsilonActions = new(StringComparer.OrdinalIgnoreCase)
    { "Start", "StepForward", "StepBackward", "ExecuteAll", "BackToStart", "Reset", "ConvertToDFA" };

    public void OnActionExecuting(ActionExecutingContext context)
    {
        if (context.ActionArguments.Count == 0) return;
        var action = context.ActionDescriptor.RouteValues.TryGetValue("action", out var a) ? a : string.Empty;
        foreach (var arg in context.ActionArguments.Values)
        {
            if (arg is AutomatonViewModel vm)
            {
                vm.EnsureInitialized();
                if (!string.IsNullOrEmpty(action) && EpsilonActions.Contains(action!)) vm.NormalizeEpsilonTransitions();
            }
        }
    }

    public void OnActionExecuted(ActionExecutedContext context) { }
}
