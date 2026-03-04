using FiniteAutomatons.Core.Models.Api;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;

namespace FiniteAutomatons.Services.Services;

public class CanvasMappingService : ICanvasMappingService
{
    public CanvasSyncResponse BuildSyncResponse(CanvasSyncRequest request)
    {
        bool isPDA = string.Equals(request.Type, "PDA", StringComparison.OrdinalIgnoreCase);

        var alphabet = request.Transitions
            .Select(t => ParseSymbol(t.Symbol))
            .Where(c => c != '\0')
            .Distinct()
            .OrderBy(c => c)
            .Select(c => c.ToString())
            .ToList();

        bool hasEpsilon = request.Transitions.Any(t => ParseSymbol(t.Symbol) == '\0');

        var states = request.States
            .OrderBy(s => s.Id)
            .Select(s => new CanvasSyncStateDto
            {
                Id = s.Id,
                IsStart = s.IsStart,
                IsAccepting = s.IsAccepting
            })
            .ToList();

        var transitions = request.Transitions
            .OrderBy(t => t.FromStateId)
            .ThenBy(t => ParseSymbol(t.Symbol))
            .ThenBy(t => t.ToStateId)
            .Select(t =>
            {
                var sym = ParseSymbol(t.Symbol);
                return new CanvasSyncTransitionDto
                {
                    FromStateId = t.FromStateId,
                    ToStateId = t.ToStateId,
                    SymbolDisplay = sym == '\0' ? "ε" : sym.ToString(),
                    IsPDA = isPDA,
                    StackPopDisplay = isPDA && t.StackPop is not null
                        ? (ParseSymbol(t.StackPop) == '\0' ? "ε" : t.StackPop)
                        : null,
                    StackPush = isPDA ? (t.StackPush ?? "") : null
                };
            })
            .ToList();

        return new CanvasSyncResponse
        {
            Alphabet = alphabet,
            HasEpsilonTransitions = hasEpsilon,
            IsPDA = isPDA,
            StateCount = states.Count,
            TransitionCount = transitions.Count,
            States = states,
            Transitions = transitions
        };
    }

    public AutomatonViewModel BuildAutomatonViewModel(CanvasSyncRequest request)
    {
        var type = request.Type?.ToUpperInvariant() switch
        {
            "NFA" => AutomatonType.NFA,
            "EPSILONNFA" => AutomatonType.EpsilonNFA,
            "PDA" => AutomatonType.PDA,
            _ => AutomatonType.DFA
        };

        var states = request.States.Select(s => new State
        {
            Id = s.Id,
            IsStart = s.IsStart,
            IsAccepting = s.IsAccepting
        }).ToList();

        var isPDA = type == AutomatonType.PDA;
        var transitions = request.Transitions.Select(t => new Transition
        {
            FromStateId = t.FromStateId,
            ToStateId = t.ToStateId,
            Symbol = ParseSymbol(t.Symbol),
            StackPop = isPDA ? ParseSymbol(t.StackPop) : null,
            StackPush = isPDA ? (t.StackPush ?? "") : null
        }).ToList();

        return new AutomatonViewModel
        {
            Type = type,
            States = states,
            Transitions = transitions,
            IsCustomAutomaton = true
        };
    }

    internal static char ParseSymbol(string? raw)
    {
        if (string.IsNullOrEmpty(raw)) return '\0';
        if (raw is "\\0" or "ε" or "epsilon" or "\0") return '\0';
        return raw[0];
    }
}
