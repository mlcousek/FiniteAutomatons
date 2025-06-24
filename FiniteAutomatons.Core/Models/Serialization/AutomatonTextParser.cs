using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using System.Text;
using System.Text.RegularExpressions;

namespace FiniteAutomatons.Core.Models.Serialization;

// Handles serialization/deserialization for the custom text format
public static class AutomatonCustomTextSerializer
{
    private const string commentPrefix = "#";
    private const string sectionPrefix = "$";

    public static Automaton Deserialize(string input)
    {
        var lines = input.Split('\n')
            .Select(l => l.Trim())
            .Where(l => !string.IsNullOrWhiteSpace(l) && !l.StartsWith(commentPrefix))
            .ToList();

        var states = new List<string>();
        var alphabet = new List<string>();
        var transitions = new List<(string from, string symbol, string to)>();
        string? initial = null;
        var accepting = new List<string>();

        string? section = null;
        foreach (var line in lines)
        {
            if (line.StartsWith(sectionPrefix))
            {
                section = line.ToLower();
                continue;
            }

            switch (section)
            {
                case "$states:":
                    states.Add(line);
                    break;
                case "$initial:":
                    initial = line;
                    break;
                case "$accepting:":
                    accepting.Add(line);
                    break;
                case "$alphabet:":
                    alphabet.Add(line);
                    break;
                case "$transitions:":
                    // Example: q0:a>q1 or q0:ε>q1
                    var match = Regex.Match(line, @"^(\w+):(.*)>(\w+)$");
                    if (match.Success)
                        transitions.Add((match.Groups[1].Value, match.Groups[2].Value, match.Groups[3].Value));
                    break;
            }
        }

        // Map state names to integer IDs for internal model
        var stateMap = states.Select((name, idx) => (name, id: idx + 1)).ToDictionary(x => x.name, x => x.id);

        // Detect automaton type
        bool hasEpsilon = transitions.Any(t => t.symbol == "ε" || t.symbol == "eps" || t.symbol == "e" || t.symbol == "\\0");
        bool isNFA = transitions
            .GroupBy(t => (t.from, t.symbol))
            .Any(g => g.Count() > 1);

        Automaton automaton;
        if (hasEpsilon)
        {
            var epsilonNfa = new EpsilonNFA();
            foreach (var (name, id) in stateMap)
            {
                epsilonNfa.AddState(new State
                {
                    Id = id,
                    IsStart = name == initial,
                    IsAccepting = accepting.Contains(name)
                });
            }
            foreach (var (from, symbol, to) in transitions)
            {
                int fromId = stateMap[from];
                int toId = stateMap[to];
                char sym = symbol == "ε" || symbol == "eps" || symbol == "e" || symbol == "\\0" ? '\0' : symbol[0];
                epsilonNfa.AddTransition(fromId, toId, sym);
            }
            epsilonNfa.SetStartState(stateMap[initial!]);
            automaton = epsilonNfa;
        }
        else if (isNFA)
        {
            var nfa = new NFA();
            foreach (var (name, id) in stateMap)
            {
                nfa.AddState(new State
                {
                    Id = id,
                    IsStart = name == initial,
                    IsAccepting = accepting.Contains(name)
                });
            }
            foreach (var (from, symbol, to) in transitions)
            {
                int fromId = stateMap[from];
                int toId = stateMap[to];
                nfa.AddTransition(fromId, toId, symbol[0]);
            }
            nfa.SetStartState(stateMap[initial!]);
            automaton = nfa;
        }
        else
        {
            var dfa = new DFA();
            foreach (var (name, id) in stateMap)
            {
                dfa.AddState(new State
                {
                    Id = id,
                    IsStart = name == initial,
                    IsAccepting = accepting.Contains(name)
                });
            }
            foreach (var (from, symbol, to) in transitions)
            {
                int fromId = stateMap[from];
                int toId = stateMap[to];
                dfa.AddTransition(fromId, toId, symbol[0]);
            }
            dfa.SetStartState(stateMap[initial!]);
            automaton = dfa;
        }

        return automaton;
    }

    public static string Serialize(Automaton automaton)
    {
        var sb = new StringBuilder();
        var stateNames = automaton.States.Select((s, i) => (s, name: $"q{i}")).ToDictionary(x => x.s.Id, x => x.name);

        sb.AppendLine("$states:");
        foreach (var name in stateNames.Values)
            sb.AppendLine(name);

        sb.AppendLine();
        sb.AppendLine("$initial:");
        var startState = automaton.States.FirstOrDefault(s => s.IsStart);
        if (startState != null)
            sb.AppendLine(stateNames[startState.Id]);

        sb.AppendLine();
        sb.AppendLine("$accepting:");
        foreach (var s in automaton.States.Where(s => s.IsAccepting))
            sb.AppendLine(stateNames[s.Id]);

        sb.AppendLine();
        sb.AppendLine("$alphabet:");
        foreach (var symbol in automaton.Transitions.Select(t => t.Symbol).Where(c => c != '\0').Distinct())
            sb.AppendLine(symbol.ToString());

        sb.AppendLine();
        sb.AppendLine("$transitions:");
        foreach (var t in automaton.Transitions)
        {
            var from = stateNames[t.FromStateId];
            var to = stateNames[t.ToStateId];
            string symbol = t.Symbol == '\0' ? "ε" : t.Symbol.ToString();
            sb.AppendLine($"{from}:{symbol}>{to}");
        }

        return sb.ToString();
    }
}
