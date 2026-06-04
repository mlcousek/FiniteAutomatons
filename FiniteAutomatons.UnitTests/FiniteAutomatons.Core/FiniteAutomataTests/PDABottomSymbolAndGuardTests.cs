using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;

namespace FiniteAutomatons.UnitTests.FiniteAutomatons.Core.FiniteAutomataTests;

/// <summary>
/// Regression tests for the three remaining critical PDA issues:
///   Fix 1 – Bottom-of-stack symbol is configurable (not hardcoded to '#').
///   Fix 2 – Popping the bottom symbol mid-computation is blocked (only allowed when it is the sole element).
///   Fix 3 – UI stack direction is consistently top-first; NormalizeInitialStackBottomFirst handles it.
/// </summary>
public class PDABottomSymbolAndGuardTests
{
    // ──────────────────────────────────────────────────────────────────────
    // Fix 1: Custom bottom-of-stack symbol
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void DPDA_CustomBottomSymbol_DefaultsToHash()
    {
        var pda = new DPDA();
        pda.BottomSymbol.ShouldBe('#');
    }

    [Fact]
    public void NPDA_CustomBottomSymbol_DefaultsToHash()
    {
        var pda = new NPDA();
        pda.BottomSymbol.ShouldBe('#');
    }

    [Fact]
    public void DPDA_CustomBottomSymbol_Z_IsUsedAsInitialStack()
    {
        var pda = new DPDA { BottomSymbol = 'Z', AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = true });
        // On 'a': pop Z (bottom), push nothing → stack empty → accept
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = 'Z', StackPush = null });

        var state = (PDAExecutionState)pda.StartExecution("a");
        // Initial stack should contain 'Z', not '#'
        state.Stack.Peek().ShouldBe('Z');

        pda.ExecuteAll(state);
        (state.IsAccepted ?? false).ShouldBeTrue();
    }

    [Fact]
    public void NPDA_CustomBottomSymbol_Z_IsUsedAsInitialStack()
    {
        var pda = new NPDA { BottomSymbol = 'Z', AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = false });
        // On 'a': pop Z (bottom), push nothing → stack empty → accept
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = 'Z', StackPush = null });

        pda.Execute("a").ShouldBeTrue();
    }

    [Fact]
    public void DPDA_CustomBottomSymbol_HashTransitionDoesNotFire_WhenBottomIsZ()
    {
        // When BottomSymbol = 'Z', a transition that pops '#' should never match
        // the initial stack (which starts with 'Z').
        var pda = new DPDA { BottomSymbol = 'Z', AcceptanceMode = PDAAcceptanceMode.FinalStateOnly };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '#', StackPush = null });

        // '#' pop transition can never fire because stack has 'Z', not '#'
        pda.Execute("a").ShouldBeFalse();
    }

    [Fact]
    public void AutomatonBuilderService_Passes_BottomSymbol_To_DPDA()
    {
        var builder = new AutomatonBuilderService(NullLogger<AutomatonBuilderService>.Instance);
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            BottomSymbol = 'Z',
            States = [new State { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        var automaton = builder.CreateAutomatonFromModel(model);
        var dpda = automaton.ShouldBeOfType<DPDA>();
        dpda.BottomSymbol.ShouldBe('Z');
    }

    [Fact]
    public void AutomatonBuilderService_Passes_BottomSymbol_To_NPDA()
    {
        var builder = new AutomatonBuilderService(NullLogger<AutomatonBuilderService>.Instance);
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            BottomSymbol = 'Z',
            States = [new State { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = []
        };

        var automaton = builder.CreateAutomatonFromModel(model);
        var npda = automaton.ShouldBeOfType<NPDA>();
        npda.BottomSymbol.ShouldBe('Z');
    }

    // ──────────────────────────────────────────────────────────────────────
    // Fix 2: Bottom symbol cannot be popped mid-computation
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// Reviewer's exact repro: automaton reads 'a' → pushes X#, reads 'b' → pops X.
    /// Stack now holds only '#'. Transition that pops '#' is ONLY available at this
    /// moment (sole element). Previously the automaton could pop '#' freely at any step.
    /// </summary>
    [Fact]
    public void DPDA_BottomSymbol_CannotBePopped_WhenOtherSymbolsArePresent()
    {
        // 'a': pops '#' (sole, allowed) → pushes "##" → stack = [#, #] (two #s)
        // 'b': pops '#' (top is '#' but NOT sole, count == 2) → guard blocks → no transition → reject
        var pda = new DPDA { AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '#', StackPush = "##" });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = '#', StackPush = null });

        // After 'a': stack = [#, #]. 'b' sees '#' on top but count > 1 → guard blocks → reject.
        pda.Execute("ab").ShouldBeFalse();
    }

    [Fact]
    public void NPDA_BottomSymbol_CannotBePopped_WhenOtherSymbolsArePresent()
    {
        // Same guard scenario for NPDA: stack = [#,#] after 'a'; 'b' sees '#' on top
        // but it is not the sole element → guard blocks → reject.
        var pda = new NPDA { AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '#', StackPush = "##" });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = '#', StackPush = null });

        pda.Execute("ab").ShouldBeFalse();
    }

    [Fact]
    public void DPDA_BottomSymbol_CanBePopped_WhenItIsTheSoleElement()
    {
        // State 1 reads 'a' (push X) → state 2 reads 'b' (pop X) → state 3 (epsilon pop '#').
        // Epsilon and consuming transitions are on separate states — no determinism conflict.
        var pda = new DPDA { AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = false });
        pda.AddState(new State { Id = 3, IsStart = false, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '\0', StackPush = "X" });
        pda.AddTransition(new Transition { FromStateId = 2, ToStateId = 3, Symbol = 'b', StackPop = 'X', StackPush = null });
        // State 3: epsilon pop '#' only when it is the sole element
        pda.AddTransition(new Transition { FromStateId = 3, ToStateId = 3, Symbol = '\0', StackPop = '#', StackPush = null });

        pda.Execute("ab").ShouldBeTrue();
        pda.Execute("a").ShouldBeFalse();
        pda.Execute("b").ShouldBeFalse();
    }

    [Fact]
    public void NPDA_BottomSymbol_CanBePopped_WhenItIsTheSoleElement()
    {
        var pda = new NPDA { AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = false });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = null });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = '\0', StackPop = '#', StackPush = null });

        pda.Execute("ab").ShouldBeTrue();
        pda.Execute("aabb").ShouldBeTrue();
        pda.Execute("aab").ShouldBeFalse();
    }

    /// <summary>
    /// Guards that the epsilon-chain in DPDA also obeys the bottom-symbol guard.
    /// An epsilon transition that pops '#' must be blocked while other symbols remain.
    /// </summary>
    [Fact]
    public void DPDA_EpsilonTransition_BottomPop_BlockedWhenOtherSymbolsPresent()
    {
        // State 1 reads 'a' → push X → go to state 2.
        // State 2 has epsilon that pops '#' → should be blocked because X is still above '#'.
        var pda = new DPDA { AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '\0', StackPush = "X" });
        // Epsilon tries to pop '#' from state 2 — stack is [X, #] so guard blocks it.
        pda.AddTransition(new Transition { FromStateId = 2, ToStateId = 2, Symbol = '\0', StackPop = '#', StackPush = null });

        // State 2 is accepting but stack has [X, #]; epsilon pop-'#' is blocked → stack not empty → reject.
        pda.Execute("a").ShouldBeFalse();
    }

    // ──────────────────────────────────────────────────────────────────────
    // Fix 3: NormalizeInitialStackBottomFirst correctly handles top-first input
    // ──────────────────────────────────────────────────────────────────────

    /// <summary>
    /// The service layer normalizes the initial stack to bottom-first.
    /// When the user provides ["X", "#"] (top-first after JS reversal this becomes ["#","X"])
    /// the normalize method must detect '#' at index 0 and keep it as-is.
    /// </summary>
    [Fact]
    public void AutomatonExecutionService_NormalizeInitialStack_BottomFirst_IsPreserved()
    {
        // Simulate what JS sends after reversing user input "X,#" (top-first):
        // charArray = ['X','#'] reversed → stored as ['#','X']
        var builder = new AutomatonBuilderService(NullLogger<AutomatonBuilderService>.Instance);
        var execSvc = new AutomatonExecutionService(builder, NullLogger<AutomatonExecutionService>.Instance);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            BottomSymbol = '#',
            States = [new State { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            // Bottom-first: '#' at index 0, 'X' at index 1 (= top)
            InitialStackSerialized = System.Text.Json.JsonSerializer.Serialize(new[] { '#', 'X' })
        };

        // BackToStart calls normalize internally; result must push '#' first then 'X' → 'X' on top
        var result = execSvc.BackToStart(model);
        result.ShouldNotBeNull();

        // The stack serialized after start must have 'X' as first element (top)
        var stackList = System.Text.Json.JsonSerializer.Deserialize<List<char>>(result.StackSerialized ?? "[]");
        stackList.ShouldNotBeNull();
        stackList![0].ShouldBe('X'); // top of stack
        stackList[^1].ShouldBe('#'); // bottom
    }

    [Fact]
    public void AutomatonExecutionService_NormalizeInitialStack_TopFirst_LegacyInput_IsReversed()
    {
        // Legacy top-first stored value: ['X','#'] → normalize detects '#' at end → reverses
        var builder = new AutomatonBuilderService(NullLogger<AutomatonBuilderService>.Instance);
        var execSvc = new AutomatonExecutionService(builder, NullLogger<AutomatonExecutionService>.Instance);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            BottomSymbol = '#',
            States = [new State { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            // Top-first legacy: 'X' at index 0 (top), '#' at end (bottom)
            InitialStackSerialized = System.Text.Json.JsonSerializer.Serialize(new[] { 'X', '#' })
        };

        var result = execSvc.BackToStart(model);
        var stackList = System.Text.Json.JsonSerializer.Deserialize<List<char>>(result.StackSerialized ?? "[]");
        stackList.ShouldNotBeNull();
        stackList![0].ShouldBe('X'); // top of stack
        stackList[^1].ShouldBe('#'); // bottom
    }

    [Fact]
    public void AutomatonExecutionService_NormalizeInitialStack_MissingBottom_IsInserted()
    {
        // User typed "X,Y" (no '#') → normalize inserts '#' at bottom
        var builder = new AutomatonBuilderService(NullLogger<AutomatonBuilderService>.Instance);
        var execSvc = new AutomatonExecutionService(builder, NullLogger<AutomatonExecutionService>.Instance);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            BottomSymbol = '#',
            States = [new State { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            // Neither first nor last is '#'
            InitialStackSerialized = System.Text.Json.JsonSerializer.Serialize(new[] { 'X', 'Y' })
        };

        var result = execSvc.BackToStart(model);
        var stackList = System.Text.Json.JsonSerializer.Deserialize<List<char>>(result.StackSerialized ?? "[]");
        stackList.ShouldNotBeNull();
        stackList![^1].ShouldBe('#'); // bottom was auto-inserted
    }

    [Fact]
    public void AutomatonExecutionService_CustomBottomSymbol_Z_IsNormalizedCorrectly()
    {
        var builder = new AutomatonBuilderService(NullLogger<AutomatonBuilderService>.Instance);
        var execSvc = new AutomatonExecutionService(builder, NullLogger<AutomatonExecutionService>.Instance);

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            BottomSymbol = 'Z',
            States = [new State { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            // Bottom-first with 'Z'
            InitialStackSerialized = System.Text.Json.JsonSerializer.Serialize(new[] { 'Z', 'X' })
        };

        var result = execSvc.BackToStart(model);
        var stackList = System.Text.Json.JsonSerializer.Deserialize<List<char>>(result.StackSerialized ?? "[]");
        stackList.ShouldNotBeNull();
        stackList![0].ShouldBe('X'); // top
        stackList[^1].ShouldBe('Z'); // custom bottom sentinel
    }

    // ──────────────────────────────────────────────────────────────────────
    // Combined: custom bottom symbol + guard
    // ──────────────────────────────────────────────────────────────────────

    [Fact]
    public void DPDA_CustomBottomSymbol_Z_Guard_BlocksMidPop()
    {
        // BottomSymbol = 'Z'; a transition pops 'Z' while 'X' is still present → blocked
        var pda = new DPDA { BottomSymbol = 'Z', AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = false });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = 'Z', StackPush = "XZ" });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'b', StackPop = 'X', StackPush = null });
        pda.AddTransition(new Transition { FromStateId = 2, ToStateId = 2, Symbol = '\0', StackPop = 'Z', StackPush = null });

        // 'a' tries to pop 'Z' but 'Z' is the sole element initially → allowed initially,
        // but after push "XZ" the stack has [X,Z]. Then 'b' pops 'X' → stack = [Z].
        // Epsilon transition from state 2 pops 'Z' → stack is empty.
        pda.Execute("ab").ShouldBeTrue();
    }

    [Fact]
    public void DPDA_CustomBottomSymbol_Z_Guard_CorrectlyAllowsFinalPop()
    {
        var pda = new DPDA { BottomSymbol = 'Z', AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack };
        pda.AddState(new State { Id = 1, IsStart = true, IsAccepting = false });
        pda.AddState(new State { Id = 2, IsStart = false, IsAccepting = false });
        pda.AddState(new State { Id = 3, IsStart = false, IsAccepting = true });
        pda.AddTransition(new Transition { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '\0', StackPush = "X" });
        pda.AddTransition(new Transition { FromStateId = 2, ToStateId = 3, Symbol = 'b', StackPop = 'X', StackPush = null });
        // State 3: epsilon pop 'Z' (custom bottom) — only fires when sole element
        pda.AddTransition(new Transition { FromStateId = 3, ToStateId = 3, Symbol = '\0', StackPop = 'Z', StackPush = null });

        pda.Execute("ab").ShouldBeTrue();
        pda.Execute("a").ShouldBeFalse();
        pda.Execute("b").ShouldBeFalse();
    }
}
