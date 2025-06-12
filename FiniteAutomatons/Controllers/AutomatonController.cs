using FiniteAutomatons.Core.Models.DoMain;
using Microsoft.AspNetCore.Mvc;
using System.Collections.Generic;

namespace FiniteAutomatons.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AutomatonController : ControllerBase
{
    [HttpPost("simulate-dfa")]
    public IActionResult SimulateDfa([FromBody] AutomatonDto dto)
    {
        var dfa = new DFA();
        dfa.States.AddRange(dto.States);
        dfa.Transitions.AddRange(dto.Transitions);
        var result = dfa.Execute(dto.Input);
        return Ok(new { accepted = result });
    }

    [HttpPost("simulate-dfa-stepwise")]
    public IActionResult SimulateDfaStepwise([FromBody] AutomatonDto dto)
    {
        var dfa = new DFA();
        dfa.States.AddRange(dto.States);
        dfa.Transitions.AddRange(dto.Transitions);
        var steps = dfa.GetStepwiseExecution(dto.Input);
        return Ok(steps);
    }

    [HttpPost("simulate-nfa")]
    public IActionResult SimulateNfa([FromBody] AutomatonDto dto)
    {
        var nfa = new NFA();
        nfa.States.AddRange(dto.States);
        nfa.Transitions.AddRange(dto.Transitions);
        var result = nfa.Execute(dto.Input);
        return Ok(new { accepted = result });
    }

    [HttpPost("simulate-nfa-stepwise")]
    public IActionResult SimulateNfaStepwise([FromBody] AutomatonDto dto)
    {
        var nfa = new NFA();
        nfa.States.AddRange(dto.States);
        nfa.Transitions.AddRange(dto.Transitions);
        var steps = nfa.GetStepwiseExecution(dto.Input);
        return Ok(steps);
    }

    [HttpPost("minimize-dfa")]
    public IActionResult MinimizeDfa([FromBody] AutomatonDto dto)
    {
        var dfa = new DFA();
        dfa.States.AddRange(dto.States);
        dfa.Transitions.AddRange(dto.Transitions);
        var minimized = DfaMinimizer.Minimize(dfa);
        return Ok(minimized);
    }

    // TODO: Add endpoints for regex, etc.
}

public class AutomatonDto
{
    public List<State> States { get; set; } = new();
    public List<Transition> Transitions { get; set; } = new();
    public string Input { get; set; } = string.Empty;
}
