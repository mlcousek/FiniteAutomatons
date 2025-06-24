using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using System.Text.Json;

namespace FiniteAutomatons.Core.Models.Serialization;

public static class AutomatonJsonSerializer
{
    private static readonly JsonSerializerOptions CachedJsonSerializerOptions = new() { WriteIndented = true };

    private class StateDto
    {
        public int Id { get; set; }
        public bool IsStart { get; set; }
        public bool IsAccepting { get; set; }
    }
    private class TransitionDto
    {
        public int FromStateId { get; set; }
        public int ToStateId { get; set; }
        public string Symbol { get; set; } = string.Empty;
    }
    private class AutomatonDto
    {
        public List<StateDto> States { get; set; } = [];
        public List<TransitionDto> Transitions { get; set; } = [];
    }

    public static string Serialize(Automaton automaton)
    {
        var dto = new AutomatonDto
        {
            States = [.. automaton.States.Select(s => new StateDto
            {
                Id = s.Id,
                IsStart = s.IsStart,
                IsAccepting = s.IsAccepting
            })],
            Transitions = [.. automaton.Transitions.Select(t => new TransitionDto
            {
                FromStateId = t.FromStateId,
                ToStateId = t.ToStateId,
                Symbol = t.Symbol == '\0' ? "ε" : t.Symbol.ToString()
            })]
        };
        return JsonSerializer.Serialize(dto, CachedJsonSerializerOptions);
    }

    public static Automaton Deserialize(string json)
    {
        var dto = JsonSerializer.Deserialize<AutomatonDto>(json, CachedJsonSerializerOptions);
        if (dto != null)
        {
            // Detect automaton type
            bool hasEpsilon = dto.Transitions.Any(t => t.Symbol == "ε" || t.Symbol == "eps" || t.Symbol == "e" || t.Symbol == "\\0");
            bool isNFA = dto.Transitions
                .GroupBy(t => (t.FromStateId, t.Symbol))
                .Any(g => g.Count() > 1);

            Automaton automaton;
            if (hasEpsilon)
            {
                var epsilonNfa = new EpsilonNFA();
                foreach (var s in dto.States)
                {
                    epsilonNfa.AddState(new State
                    {
                        Id = s.Id,
                        IsStart = s.IsStart,
                        IsAccepting = s.IsAccepting
                    });
                }
                foreach (var t in dto.Transitions)
                {
                    char sym = t.Symbol == "ε" || t.Symbol == "eps" || t.Symbol == "e" || t.Symbol == "\\0" ? '\0' : t.Symbol[0];
                    epsilonNfa.AddTransition(t.FromStateId, t.ToStateId, sym);
                }
                var start = dto.States.FirstOrDefault(s => s.IsStart);
                if (start != null)
                    epsilonNfa.SetStartState(start.Id);
                automaton = epsilonNfa;
            }
            else if (isNFA)
            {
                var nfa = new NFA();
                foreach (var s in dto.States)
                {
                    nfa.AddState(new State
                    {
                        Id = s.Id,
                        IsStart = s.IsStart,
                        IsAccepting = s.IsAccepting
                    });
                }
                foreach (var t in dto.Transitions)
                {
                    nfa.AddTransition(t.FromStateId, t.ToStateId, t.Symbol[0]);
                }
                var start = dto.States.FirstOrDefault(s => s.IsStart);
                if (start != null)
                    nfa.SetStartState(start.Id);
                automaton = nfa;
            }
            else
            {
                var dfa = new DFA();
                foreach (var s in dto.States)
                {
                    dfa.AddState(new State
                    {
                        Id = s.Id,
                        IsStart = s.IsStart,
                        IsAccepting = s.IsAccepting
                    });
                }
                foreach (var t in dto.Transitions)
                {
                    dfa.AddTransition(t.FromStateId, t.ToStateId, t.Symbol[0]);
                }
                var start = dto.States.FirstOrDefault(s => s.IsStart);
                if (start != null)
                    dfa.SetStartState(start.Id);
                automaton = dfa;
            }
            return automaton;
        }

        throw new InvalidOperationException("Invalid JSON automaton definition.");
    }
}
