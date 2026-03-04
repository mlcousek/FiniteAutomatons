using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FiniteAutomatons.Services.Services;

public sealed class RegexToAutomatonService(ILogger<RegexToAutomatonService> logger) : IRegexToAutomatonService
{
    private readonly ILogger<RegexToAutomatonService> logger = logger;

    public EpsilonNFA BuildEpsilonNfaFromRegex(string regex)
    {
        ArgumentNullException.ThrowIfNull(regex);
        var tokens = Tokenize(regex);
        var postfix = ToPostfix(tokens);
        var enfa = BuildFromPostfix(postfix);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Built EpsilonNFA from regex '{Regex}' with {States} states and {Transitions} transitions",
            regex, enfa.States.Count, enfa.Transitions.Count);
        }
        return enfa;
    }

    private enum TokType { Char, CharClass, Star, Plus, Question, Or, Open, Close, Concat }
    private sealed record Tok(TokType Type, string? Value);

    private static IEnumerable<Tok> Tokenize(string s)
    {
        var outTokens = ParseTokens(s);
        return InsertConcatenationTokens(outTokens);
    }

    private static List<Tok> ParseTokens(string s)
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

            if (c == '\\')
            {
                escape = true;
                continue;
            }

            if (IsAnchorCharacter(c, i, s.Length))
                continue;

            if (c == '[')
            {
                var (charClassToken, endIndex) = ParseCharacterClass(s, i);
                outTokens.Add(charClassToken);
                i = endIndex;
                continue;
            }

            outTokens.Add(CreateOperatorToken(c));
        }

        if (escape)
            throw new ArgumentException("Trailing escape character in regular expression");

        return outTokens;
    }

    private static bool IsAnchorCharacter(char c, int index, int length)
    {
        return (c == '^' && index == 0) || (c == '$' && index == length - 1);
    }

    private static (Tok Token, int EndIndex) ParseCharacterClass(string s, int startIndex)
    {
        int j = startIndex + 1;
        var classChars = new List<char>();
        bool negated = false;

        if (j < s.Length && s[j] == '^')
        {
            negated = true;
            j++;
        }

        while (j < s.Length && s[j] != ']')
        {
            if (TryParseEscapedChar(s, j, out var escapedChar, out var charsConsumed))
            {
                classChars.Add(escapedChar);
                j += charsConsumed;
                continue;
            }

            if (TryParseCharRange(s, j, out var rangeChars, out charsConsumed))
            {
                classChars.AddRange(rangeChars);
                j += charsConsumed;
                continue;
            }

            classChars.Add(s[j]);
            j++;
        }

        ValidateCharacterClass(j, s.Length, classChars.Count, negated);

        var val = new string([.. classChars.Distinct()]);
        return (new Tok(TokType.CharClass, val), j);
    }

    private static bool TryParseEscapedChar(string s, int index, out char escapedChar, out int charsConsumed)
    {
        if (s[index] == '\\' && index + 1 < s.Length)
        {
            escapedChar = s[index + 1];
            charsConsumed = 2;
            return true;
        }

        escapedChar = default;
        charsConsumed = 0;
        return false;
    }

    private static bool TryParseCharRange(string s, int index, out List<char> rangeChars, out int charsConsumed)
    {
        rangeChars = [];
        charsConsumed = 0;

        if (index + 2 < s.Length && s[index + 1] == '-' && s[index + 2] != ']')
        {
            char start = s[index];
            char end = s[index + 2];

            if (start > end)
                throw new ArgumentException($"Invalid range '{start}-{end}' in character class");

            for (char cc = start; cc <= end; cc++)
                rangeChars.Add(cc);

            charsConsumed = 3;
            return true;
        }

        return false;
    }

    private static void ValidateCharacterClass(int endIndex, int stringLength, int charCount, bool negated)
    {
        if (endIndex >= stringLength || endIndex < 0)
            throw new ArgumentException("Unterminated character class");

        if (charCount == 0)
            throw new ArgumentException("Empty character class");

        if (negated)
            throw new ArgumentException("Negated character classes are not supported");
    }

    private static Tok CreateOperatorToken(char c)
    {
        return c switch
        {
            '*' => new Tok(TokType.Star, null),
            '+' => new Tok(TokType.Plus, null),
            '?' => new Tok(TokType.Question, null),
            '|' => new Tok(TokType.Or, null),
            '(' => new Tok(TokType.Open, null),
            ')' => new Tok(TokType.Close, null),
            _ => new Tok(TokType.Char, c.ToString())
        };
    }

    private static List<Tok> InsertConcatenationTokens(List<Tok> tokens)
    {
        var withConcat = new List<Tok>();

        for (int i = 0; i < tokens.Count; i++)
        {
            withConcat.Add(tokens[i]);

            if (i + 1 < tokens.Count && ShouldInsertConcat(tokens[i].Type, tokens[i + 1].Type))
            {
                withConcat.Add(new Tok(TokType.Concat, null));
            }
        }

        return withConcat;
    }

    private static bool ShouldInsertConcat(TokType current, TokType next)
    {
        bool currentCanBeBefore = current is TokType.Char or TokType.CharClass or TokType.Star
            or TokType.Plus or TokType.Question or TokType.Close;

        bool nextCanBeAfter = next is TokType.Char or TokType.CharClass or TokType.Open;

        return currentCanBeBefore && nextCanBeAfter;
    }

    private static readonly Dictionary<TokType, int> Prec = new()
    {
        [TokType.Star] = 5,
        [TokType.Plus] = 5,
        [TokType.Question] = 5,
        [TokType.Concat] = 4,
        [TokType.Or] = 3
    };

    private static List<Tok> ToPostfix(IEnumerable<Tok> tokens)
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

    private sealed class Fragment
    {
        public int Start { get; set; }
        public int End { get; set; }
    }

    private static EpsilonNFA BuildFromPostfix(List<Tok> postfix)
    {
        var context = new NfaBuildContext();
        var fragStack = new Stack<Fragment>();

        foreach (var t in postfix)
        {
            var fragment = t.Type switch
            {
                TokType.Char => BuildCharFragment(context, t),
                TokType.CharClass => BuildCharClassFragment(context, t),
                TokType.Concat => BuildConcatFragment(context, fragStack),
                TokType.Or => BuildOrFragment(context, fragStack),
                TokType.Star => BuildStarFragment(context, fragStack),
                TokType.Plus => BuildPlusFragment(context, fragStack),
                TokType.Question => BuildQuestionFragment(context, fragStack),
                _ => throw new InvalidOperationException($"Unsupported token in postfix: {t}")
            };

            fragStack.Push(fragment);
        }

        return FinalizeNfa(context.Enfa, fragStack);
    }

    private sealed class NfaBuildContext
    {
        public EpsilonNFA Enfa { get; } = new();
        private int nextId = 1;

        public int NewState()
        {
            var s = new State { Id = nextId++ };
            Enfa.AddState(s);
            return s.Id;
        }
    }

    private static Fragment BuildCharFragment(NfaBuildContext context, Tok token)
    {
        int start = context.NewState();
        int end = context.NewState();
        char ch = token.Value![0];
        context.Enfa.AddTransition(start, end, ch);
        return new Fragment { Start = start, End = end };
    }

    private static Fragment BuildCharClassFragment(NfaBuildContext context, Tok token)
    {
        int start = context.NewState();
        int end = context.NewState();
        var chars = token.Value!.ToCharArray();

        foreach (var ch in chars.Distinct())
        {
            context.Enfa.AddTransition(start, end, ch);
        }

        return new Fragment { Start = start, End = end };
    }

    private static Fragment BuildConcatFragment(NfaBuildContext context, Stack<Fragment> fragStack)
    {
        var f2 = fragStack.Pop();
        var f1 = fragStack.Pop();
        context.Enfa.AddEpsilonTransition(f1.End, f2.Start);
        return new Fragment { Start = f1.Start, End = f2.End };
    }

    private static Fragment BuildOrFragment(NfaBuildContext context, Stack<Fragment> fragStack)
    {
        var f2 = fragStack.Pop();
        var f1 = fragStack.Pop();
        int start = context.NewState();
        int end = context.NewState();

        context.Enfa.AddEpsilonTransition(start, f1.Start);
        context.Enfa.AddEpsilonTransition(start, f2.Start);
        context.Enfa.AddEpsilonTransition(f1.End, end);
        context.Enfa.AddEpsilonTransition(f2.End, end);

        return new Fragment { Start = start, End = end };
    }

    private static Fragment BuildStarFragment(NfaBuildContext context, Stack<Fragment> fragStack)
    {
        var f = fragStack.Pop();
        int start = context.NewState();
        int end = context.NewState();

        context.Enfa.AddEpsilonTransition(start, f.Start);
        context.Enfa.AddEpsilonTransition(start, end);
        context.Enfa.AddEpsilonTransition(f.End, f.Start);
        context.Enfa.AddEpsilonTransition(f.End, end);

        return new Fragment { Start = start, End = end };
    }

    private static Fragment BuildPlusFragment(NfaBuildContext context, Stack<Fragment> fragStack)
    {
        var f = fragStack.Pop();
        int start = context.NewState();
        int end = context.NewState();

        context.Enfa.AddEpsilonTransition(start, f.Start);
        context.Enfa.AddEpsilonTransition(f.End, f.Start);
        context.Enfa.AddEpsilonTransition(f.End, end);

        return new Fragment { Start = start, End = end };
    }

    private static Fragment BuildQuestionFragment(NfaBuildContext context, Stack<Fragment> fragStack)
    {
        var f = fragStack.Pop();
        int start = context.NewState();
        int end = context.NewState();

        context.Enfa.AddEpsilonTransition(start, f.Start);
        context.Enfa.AddEpsilonTransition(start, end);
        context.Enfa.AddEpsilonTransition(f.End, end);

        return new Fragment { Start = start, End = end };
    }

    private static EpsilonNFA FinalizeNfa(EpsilonNFA enfa, Stack<Fragment> fragStack)
    {
        if (fragStack.Count != 1)
            throw new ArgumentException("Invalid regular expression");

        var top = fragStack.Pop();
        enfa.SetStartState(top.Start);
        var endState = enfa.States.First(s => s.Id == top.End);
        endState.IsAccepting = true;

        return enfa;
    }

    public static object DescribeEnfa(EpsilonNFA enfa)
    {
        return new
        {
            States = enfa.States.Select(s => new { s.Id, s.IsStart, s.IsAccepting }).ToList(),
            Transitions = enfa.Transitions.Select(t => new { t.FromStateId, t.ToStateId, Symbol = t.Symbol == '\0' ? "?" : t.Symbol.ToString() }).ToList()
        };
    }
}
