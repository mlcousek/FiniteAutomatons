using System;
using System.Text;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Extensions.Logging;
using FiniteAutomatons.Services.Interfaces;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.DoMain;

namespace FiniteAutomatons.Services.Services;

/// <summary>
/// Very small Thompson construction based converter from limited regular expression syntax to an EpsilonNFA.
/// Supported operators: concatenation, '|', '*', '+', '?', and parentheses '()'.
/// Also supports character classes like [abc] and ranges like [a-z].
/// Literals are any character except the operators and parentheses. Escape with backslash for operators.
/// Anchors '^' and '$' are accepted at start/end but do not change automaton semantics (full-match automaton already enforces that).
/// This implementation is intentionally small and defensive for use in the application tests.
/// </summary>
public sealed class RegexToAutomatonService(ILogger<RegexToAutomatonService> logger) : IRegexToAutomatonService
{
    private readonly ILogger<RegexToAutomatonService> logger = logger;

    public EpsilonNFA BuildEpsilonNfaFromRegex(string regex)
    {
        if (regex == null) throw new ArgumentNullException(nameof(regex));
        // Tokenize and convert to postfix (Shunting-yard) then build via Thompson
        var tokens = Tokenize(regex).ToList();
        var postfix = ToPostfix(tokens);
        var enfa = BuildFromPostfix(postfix);
        logger.LogInformation("Built EpsilonNFA from regex '{Regex}' with {States} states and {Transitions} transitions",
            regex, enfa.States.Count, enfa.Transitions.Count);
        return enfa;
    }

    // --- Tokenization ---
    private enum TokType { Char, CharClass, Star, Plus, Question, Or, Open, Close, Concat }
    private sealed record Tok(TokType Type, string? Value);

    private IEnumerable<Tok> Tokenize(string s)
    {
        var outTokens = new List<Tok>();
        bool escape = false;
        for (int i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (escape)
            {
                outTokens.Add(new Tok(TokType.Char, c.ToString()));
                escape = false;
                continue;
            }
            if (c == '\\') { escape = true; continue; }

            // Anchors: '^' at start and '$' at end are acknowledged but ignored for construction
            if (c == '^' && i == 0)
            {
                // ignore as automaton always starts at beginning
                continue;
            }
            if (c == '$' && i == s.Length - 1)
            {
                // ignore as automaton matches full input by design
                continue;
            }

            if (c == '[')
            {
                // parse character class until closing ']'
                var j = i + 1;
                var classChars = new List<char>();
                bool negated = false;
                if (j < s.Length && s[j] == '^')
                {
                    // negation not supported in construction; treat as literal '^' if present
                    negated = true;
                    j++;
                }
                while (j < s.Length && s[j] != ']')
                {
                    if (s[j] == '\\' && j + 1 < s.Length)
                    {
                        classChars.Add(s[j + 1]);
                        j += 2;
                        continue;
                    }
                    // range a-z
                    if (j + 2 < s.Length && s[j + 1] == '-' && s[j + 2] != ']')
                    {
                        char start = s[j];
                        char end = s[j + 2];
                        if (start > end) throw new ArgumentException($"Invalid range '{start}-{end}' in character class");
                        for (char cc = start; cc <= end; cc++) classChars.Add(cc);
                        j += 3;
                        continue;
                    }
                    classChars.Add(s[j]);
                    j++;
                }
                if (j >= s.Length || s[j] != ']') throw new ArgumentException("Unterminated character class");
                if (classChars.Count == 0) throw new ArgumentException("Empty character class");
                // if negated, we currently don't support negation - it's an error
                if (negated) throw new ArgumentException("Negated character classes are not supported");
                var val = new string(classChars.Distinct().ToArray());
                outTokens.Add(new Tok(TokType.CharClass, val));
                i = j; // advance to closing ']'
                continue;
            }

            switch (c)
            {
                case '*': outTokens.Add(new Tok(TokType.Star, null)); break;
                case '+': outTokens.Add(new Tok(TokType.Plus, null)); break;
                case '?': outTokens.Add(new Tok(TokType.Question, null)); break;
                case '|': outTokens.Add(new Tok(TokType.Or, null)); break;
                case '(':
                    outTokens.Add(new Tok(TokType.Open, null)); break;
                case ')':
                    outTokens.Add(new Tok(TokType.Close, null)); break;
                default:
                    outTokens.Add(new Tok(TokType.Char, c.ToString())); break;
            }
        }

        // Insert explicit concatenation tokens where appropriate
        var withConcat = new List<Tok>();
        for (int i = 0; i < outTokens.Count; i++)
        {
            var t = outTokens[i];
            withConcat.Add(t);
            if (i + 1 < outTokens.Count)
            {
                var a = t.Type;
                var b = outTokens[i + 1].Type;
                bool aCanBeBefore = a == TokType.Char || a == TokType.CharClass || a == TokType.Star || a == TokType.Plus || a == TokType.Question || a == TokType.Close;
                bool bCanBeAfter = b == TokType.Char || b == TokType.CharClass || b == TokType.Open;
                if (aCanBeBefore && bCanBeAfter)
                {
                    withConcat.Add(new Tok(TokType.Concat, null));
                }
            }
        }

        return withConcat;
    }

    // --- Shunting-yard to postfix ---
    private static readonly Dictionary<TokType, int> Prec = new()
    {
        [TokType.Star] = 5,
        [TokType.Plus] = 5,
        [TokType.Question] = 5,
        [TokType.Concat] = 4,
        [TokType.Or] = 3
    };

    private List<Tok> ToPostfix(IEnumerable<Tok> tokens)
    {
        var output = new List<Tok>();
        var stack = new Stack<Tok>();
        foreach (var tok in tokens)
        {
            switch (tok.Type)
            {
                case TokType.Char:
                case TokType.CharClass:
                    output.Add(tok);
                    break;
                case TokType.Star:
                case TokType.Plus:
                case TokType.Question:
                case TokType.Concat:
                case TokType.Or:
                    while (stack.Count > 0 && stack.Peek().Type != TokType.Open && Prec[stack.Peek().Type] >= Prec[tok.Type])
                    {
                        output.Add(stack.Pop());
                    }
                    stack.Push(tok);
                    break;
                case TokType.Open:
                    stack.Push(tok);
                    break;
                case TokType.Close:
                    while (stack.Count > 0 && stack.Peek().Type != TokType.Open)
                        output.Add(stack.Pop());
                    if (stack.Count == 0) throw new ArgumentException("Mismatched parentheses in regex");
                    stack.Pop();
                    break;
            }
        }
        while (stack.Count > 0)
        {
            var t = stack.Pop();
            if (t.Type == TokType.Open || t.Type == TokType.Close) throw new ArgumentException("Mismatched parentheses in regex");
            output.Add(t);
        }
        return output;
    }

    // --- Thompson construction stack fragments ---
    private sealed class Fragment
    {
        public int Start { get; set; }
        public int End { get; set; }
    }

    private EpsilonNFA BuildFromPostfix(List<Tok> postfix)
    {
        var enfa = new EpsilonNFA();
        int nextId = 1;
        var fragStack = new Stack<Fragment>();

        int NewState()
        {
            var s = new State { Id = nextId++ };
            enfa.AddState(s);
            return s.Id;
        }

        foreach (var t in postfix)
        {
            switch (t.Type)
            {
                case TokType.Char:
                    {
                        int s = NewState();
                        int e = NewState();
                        char ch = t.Value![0];
                        enfa.AddTransition(s, e, ch);
                        fragStack.Push(new Fragment { Start = s, End = e });
                    }
                    break;
                case TokType.CharClass:
                    {
                        int s = NewState();
                        int e = NewState();
                        var chars = t.Value!.ToCharArray();
                        foreach (var ch in chars.Distinct())
                        {
                            enfa.AddTransition(s, e, ch);
                        }
                        fragStack.Push(new Fragment { Start = s, End = e });
                    }
                    break;
                case TokType.Concat:
                    {
                        var f2 = fragStack.Pop();
                        var f1 = fragStack.Pop();
                        // connect f1.End -> f2.Start via epsilon
                        enfa.AddEpsilonTransition(f1.End, f2.Start);
                        fragStack.Push(new Fragment { Start = f1.Start, End = f2.End });
                    }
                    break;
                case TokType.Or:
                    {
                        var f2 = fragStack.Pop();
                        var f1 = fragStack.Pop();
                        int s = NewState();
                        int e = NewState();
                        enfa.AddEpsilonTransition(s, f1.Start);
                        enfa.AddEpsilonTransition(s, f2.Start);
                        enfa.AddEpsilonTransition(f1.End, e);
                        enfa.AddEpsilonTransition(f2.End, e);
                        fragStack.Push(new Fragment { Start = s, End = e });
                    }
                    break;
                case TokType.Star:
                    {
                        var f = fragStack.Pop();
                        int s = NewState();
                        int e = NewState();
                        enfa.AddEpsilonTransition(s, f.Start);
                        enfa.AddEpsilonTransition(s, e);
                        enfa.AddEpsilonTransition(f.End, f.Start);
                        enfa.AddEpsilonTransition(f.End, e);
                        fragStack.Push(new Fragment { Start = s, End = e });
                    }
                    break;
                case TokType.Plus:
                    {
                        var f = fragStack.Pop();
                        int s = NewState();
                        int e = NewState();
                        enfa.AddEpsilonTransition(s, f.Start);
                        enfa.AddEpsilonTransition(f.End, f.Start);
                        enfa.AddEpsilonTransition(f.End, e);
                        fragStack.Push(new Fragment { Start = s, End = e });
                    }
                    break;
                case TokType.Question:
                    {
                        var f = fragStack.Pop();
                        int s = NewState();
                        int e = NewState();
                        enfa.AddEpsilonTransition(s, f.Start);
                        enfa.AddEpsilonTransition(s, e);
                        enfa.AddEpsilonTransition(f.End, e);
                        fragStack.Push(new Fragment { Start = s, End = e });
                    }
                    break;
                default:
                    throw new InvalidOperationException($"Unsupported token in postfix: {t}");
            }
        }

        if (fragStack.Count != 1) throw new ArgumentException("Invalid regular expression");
        var top = fragStack.Pop();
        enfa.SetStartState(top.Start);
        // mark end as accepting
        var endState = enfa.States.First(s => s.Id == top.End);
        endState.IsAccepting = true;
        return enfa;
    }

    // Expose a small helper to serialize to a view model-like DTO for tests/integration
    public object DescribeEnfa(EpsilonNFA enfa)
    {
        return new
        {
            States = enfa.States.Select(s => new { s.Id, s.IsStart, s.IsAccepting }).ToList(),
            Transitions = enfa.Transitions.Select(t => new { t.FromStateId, t.ToStateId, Symbol = t.Symbol == '\0' ? "?" : t.Symbol.ToString() }).ToList()
        };
    }
}
