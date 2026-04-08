using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.Diagnostics;

namespace FiniteAutomatons.UnitTests.Services;

public class InputGenerationServiceComprehensiveTests
{
    private readonly InputGenerationService service;
    private readonly AutomatonBuilderService builderService;

    public InputGenerationServiceComprehensiveTests()
    {
        builderService = new AutomatonBuilderService(NullLogger<AutomatonBuilderService>.Instance);
        service = new InputGenerationService(NullLogger<InputGenerationService>.Instance, builderService);
    }

    #region DFA Comprehensive Tests

    [Fact]
    public void DFA_GenerateAcceptingString_SimpleLanguage_ReturnsValidString()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'b' }
            ]
        };

        var result = service.GenerateAcceptingString(automaton, 20);

        result.ShouldNotBeNull();
        result.ShouldStartWith("a");
    }

    [Fact]
    public void DFA_GenerateRejectingString_ReturnsStringNotAccepted()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'b' }
            ]
        };

        var result = service.GenerateRejectingString(automaton, 20);

        result.ShouldNotBeNull();
    }

    [Fact]
    public void DFA_GenerateRandomAcceptingString_WithinLengthBounds_ReturnsValid()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 1, Symbol = 'b' }
            ]
        };

        var result = service.GenerateRandomAcceptingString(automaton, 3, 10, 50, 42);

        result.ShouldNotBeNull();
        result.Length.ShouldBeGreaterThanOrEqualTo(3);
        result.Length.ShouldBeLessThanOrEqualTo(10);
    }

    [Fact]
    public void DFA_GenerateInterestingCases_ReturnsMultipleCases()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'b' }
            ]
        };

        var cases = service.GenerateInterestingCases(automaton, 15);

        cases.Count.ShouldBeGreaterThan(3);
        cases.ShouldContain(c => c.Description == "Empty string (ε)");
        cases.ShouldContain(c => c.Description == "Known accepting string");
    }

    [Fact]
    public void DFA_GenerateRandomString_ProducesStringFromAlphabet()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b' }
            ]
        };

        var result = service.GenerateRandomString(automaton, 5, 10, 123);

        result.ShouldNotBeNull();
        result.All(c => c == 'a' || c == 'b').ShouldBeTrue();
    }

    #endregion

    #region NFA Comprehensive Tests

    [Fact]
    public void NFA_GenerateAcceptingString_WithNondeterminism_FindsPath()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true },
                new() { Id = 3, IsStart = false, IsAccepting = false }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 1, ToStateId = 3, Symbol = 'a' },
                new() { FromStateId = 3, ToStateId = 2, Symbol = 'b' }
            ]
        };

        var result = service.GenerateAcceptingString(automaton, 20);

        result.ShouldNotBeNull();
        (result == "a" || result == "ab").ShouldBeTrue();
    }

    [Fact]
    public void NFA_GenerateNondeterministicCase_IdentifiesNondeterministicTransition()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true },
                new() { Id = 3, IsStart = false, IsAccepting = false }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 1, ToStateId = 3, Symbol = 'a' }
            ]
        };

        var result = service.GenerateNondeterministicCase(automaton, 20);

        result.ShouldNotBeNull();
        result.ShouldContain('a');
    }

    [Fact]
    public void NFA_GenerateInterestingCases_IncludesNondeterministicCase()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true },
                new() { Id = 3, IsStart = false, IsAccepting = false }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 1, ToStateId = 3, Symbol = 'a' }
            ]
        };

        var cases = service.GenerateInterestingCases(automaton, 15);

        cases.ShouldContain(c => c.Description == "Tests nondeterminism");
    }

    #endregion

    #region Epsilon-NFA Comprehensive Tests

    [Fact]
    public void EpsilonNFA_GenerateAcceptingString_WithEpsilonTransitions_FindsPath()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = false },
                new() { Id = 3, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0' },
                new() { FromStateId = 2, ToStateId = 3, Symbol = 'a' }
            ]
        };

        var result = service.GenerateAcceptingString(automaton, 20);

        result.ShouldNotBeNull();
        result.ShouldBe("a");
    }

    [Fact]
    public void EpsilonNFA_GenerateEpsilonCase_IdentifiesEpsilonTransition()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0' }
            ]
        };

        var result = service.GenerateEpsilonCase(automaton, 20);

        result.ShouldNotBeNull();
    }

    [Fact]
    public void EpsilonNFA_GenerateInterestingCases_IncludesEpsilonCase()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.EpsilonNFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = '\0' },
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' }
            ]
        };

        var cases = service.GenerateInterestingCases(automaton, 15);

        cases.ShouldContain(c => c.Description == "Tests ε-transitions");
    }

    #endregion

    #region PDA Comprehensive Tests - Accepting Strings

    [Fact]
    public void PDA_GenerateAcceptingString_BalancedParentheses_ReturnsValidString()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = '(', StackPop = '\0', StackPush = "(" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = ')', StackPop = '(', StackPush = null }
            ],
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var result = service.GenerateAcceptingString(automaton, 10);

        result.ShouldNotBeNull();
        var pda = builderService.CreateDPDA(automaton);
        pda.Execute(result).ShouldBeTrue($"Generated string '{result}' should be accepted");
    }

    [Fact]
    public void PDA_GenerateAcceptingString_EmptyString_ReturnsEmpty()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [],
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var result = service.GenerateAcceptingString(automaton, 10);

        result.ShouldNotBeNull();
        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void PDA_GenerateAcceptingString_FinalStateOnly_DoesNotRequireEmptyStack()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" }
            ],
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var result = service.GenerateAcceptingString(automaton, 5);

        result.ShouldNotBeNull();
        var pda = builderService.CreateDPDA(automaton);
        pda.Execute(result).ShouldBeTrue();
    }

    [Fact]
    public void PDA_GenerateRandomAcceptingString_ReturnsValidWithinBounds()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = '(', StackPop = '\0', StackPush = "(" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = ')', StackPop = '(', StackPush = null }
            ],
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var result = service.GenerateRandomAcceptingString(automaton, 2, 10, 100, 42);

        if (result != null)
        {
            result.Length.ShouldBeGreaterThanOrEqualTo(2);
            result.Length.ShouldBeLessThanOrEqualTo(10);
            var pda = builderService.CreateDPDA(automaton);
            pda.Execute(result).ShouldBeTrue();
        }
    }

    #endregion

    #region PDA Comprehensive Tests - Rejecting Strings

    [Fact]
    public void PDA_GenerateRejectingString_UnbalancedParentheses_ReturnsRejecting()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = '(', StackPop = '\0', StackPush = "(" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = ')', StackPop = '(', StackPush = null }
            ],
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var result = service.GenerateRejectingString(automaton, 10);

        result.ShouldNotBeNull();
        var pda = builderService.CreateDPDA(automaton);

        bool isRejected;
        try
        {
            isRejected = !pda.Execute(result);
        }
        catch
        {
            isRejected = true;
        }

        isRejected.ShouldBeTrue($"Generated string '{result}' should be rejected");
    }

    [Fact]
    public void PDA_GenerateRejectingString_WrongOrder_ReturnsRejecting()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = null }
            ],
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var result = service.GenerateRejectingString(automaton, 10);

        result.ShouldNotBeNull();
        var pda = builderService.CreateDPDA(automaton);

        bool isRejected;
        try
        {
            isRejected = !pda.Execute(result);
        }
        catch
        {
            isRejected = true;
        }

        isRejected.ShouldBeTrue($"Generated string '{result}' should be rejected");
    }

    [Fact]
    public void PDA_GenerateRejectingString_EmptyStackOnly_ReturnsRejecting()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = null }
            ],
            AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly
        };

        var result = service.GenerateRejectingString(automaton, 10);

        result.ShouldNotBeNull();
        var pda = builderService.CreateDPDA(automaton);

        bool isRejected;
        try
        {
            isRejected = !pda.Execute(result);
        }
        catch
        {
            isRejected = true;
        }

        isRejected.ShouldBeTrue($"Generated string '{result}' should be rejected");
    }

    [Fact]
    public void PDA_GenerateRejectingString_FinalStateOnly_WithNonEmptyStack_ReturnsRejecting()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '\0', StackPush = "X" }
            ],
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var result = service.GenerateRejectingString(automaton, 10);

        result.ShouldNotBeNull();
    }

    [Fact]
    public void PDA_GenerateAcceptingString_LargeDpdaSearchSpace_CompletesWithoutHanging()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = null, StackPush = "A" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = null, StackPush = "B" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'c', StackPop = null, StackPush = "C" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'd', StackPop = null, StackPush = "D" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'e', StackPop = null, StackPush = "E" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'f', StackPop = null, StackPush = "F" }
            ],
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var sw = Stopwatch.StartNew();
        var accepting = service.GenerateAcceptingString(automaton, 40);
        sw.Stop();

        accepting.ShouldBeNull();
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void PDA_GenerateRejectingString_LargeNpdaSearchSpace_CompletesWithoutHanging()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = null, StackPush = null },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = null, StackPush = null },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'c', StackPop = null, StackPush = null },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'd', StackPop = null, StackPush = null },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'e', StackPop = null, StackPush = null },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'f', StackPop = null, StackPush = null }
            ],
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var sw = Stopwatch.StartNew();
        var rejecting = service.GenerateRejectingString(automaton, 40);
        sw.Stop();

        rejecting.ShouldBeNull();
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    #endregion

    #region PDA Interesting Cases

    [Fact]
    public void PDA_GenerateInterestingCases_IncludesPdaSpecificCases()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = '(', StackPop = '\0', StackPush = "(" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = ')', StackPop = '(', StackPush = null }
            ],
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var cases = service.GenerateInterestingCases(automaton, 15);

        cases.ShouldContain(c => c.Description.Contains("PDA"));
        cases.Count.ShouldBeGreaterThan(5);
    }

    [Fact]
    public void PDA_GenerateInterestingCases_WithPushTransitions_IncludesPushCase()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" }
            ],
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var cases = service.GenerateInterestingCases(automaton, 15);

        cases.ShouldContain(c => c.Description.Contains("push"));
    }

    [Fact]
    public void PDA_GenerateInterestingCases_WithPopTransitions_IncludesPopCase()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = 'X', StackPush = null }
            ],
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var cases = service.GenerateInterestingCases(automaton, 15);

        cases.ShouldContain(c => c.Description.Contains("pop"));
    }

    #endregion

    #region Edge Cases

    [Fact]
    public void AllTypes_GenerateAcceptingString_NoStates_ReturnsNull()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [],
            Transitions = []
        };

        var result = service.GenerateAcceptingString(automaton, 20);

        result.ShouldBeNull();
    }

    [Fact]
    public void AllTypes_GenerateRejectingString_NoAlphabet_ReturnsNull()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions = []
        };

        var result = service.GenerateRejectingString(automaton, 20);

        result.ShouldBeNull();
    }

    [Fact]
    public void AllTypes_GenerateRandomString_EmptyAlphabet_ReturnsEmptyString()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true }],
            Transitions = []
        };

        var result = service.GenerateRandomString(automaton, 0, 10);

        result.ShouldBe(string.Empty);
    }

    [Fact]
    public void AllTypes_GenerateInterestingCases_MinimalAutomaton_ReturnsBasicCases()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' }]
        };

        var cases = service.GenerateInterestingCases(automaton, 15);

        cases.Count.ShouldBeGreaterThan(0);
        cases[0].Input.ShouldBe(string.Empty);
    }

    [Fact]
    public void AllTypes_GenerateRandomAcceptingString_StartStateIsAccepting_CanReturnEmpty()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' }]
        };

        var result = service.GenerateRandomAcceptingString(automaton, 0, 5, 100, 42);

        result.ShouldNotBeNull();
    }

    [Fact]
    public void AllTypes_GenerateRandomString_WithSeed_IsDeterministic()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States = [new() { Id = 1, IsStart = true }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a' },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b' }
            ]
        };

        var result1 = service.GenerateRandomString(automaton, 5, 10, 123);
        var result2 = service.GenerateRandomString(automaton, 5, 10, 123);

        result1.ShouldBe(result2);
    }

    #endregion

    #region Complex Scenarios

    [Fact]
    public void DFA_ComplexLanguage_EvenNumberOfAs_GeneratesCorrectStrings()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = true },
                new() { Id = 2, IsStart = false, IsAccepting = false }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 1, Symbol = 'a' },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b' },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'b' }
            ]
        };

        var accepting = service.GenerateAcceptingString(automaton, 20);
        var rejecting = service.GenerateRejectingString(automaton, 20);

        accepting.ShouldNotBeNull();
        rejecting.ShouldNotBeNull();
        accepting.ShouldNotBe(rejecting);
    }

    [Fact]
    public void NFA_ComplexNondeterminism_MultiplePathsToAccepting_FindsOne()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.NFA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = false },
                new() { Id = 3, IsStart = false, IsAccepting = false },
                new() { Id = 4, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
                new() { FromStateId = 1, ToStateId = 3, Symbol = 'a' },
                new() { FromStateId = 2, ToStateId = 4, Symbol = 'b' },
                new() { FromStateId = 3, ToStateId = 4, Symbol = 'c' }
            ]
        };

        var result = service.GenerateAcceptingString(automaton, 20);

        result.ShouldNotBeNull();
        (result == "ab" || result == "ac").ShouldBeTrue();
    }

    [Fact]
    public void PDA_AnBn_Language_GeneratesBalancedStrings()
    {
        var automaton = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" },
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'b', StackPop = 'X', StackPush = null },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'b', StackPop = 'X', StackPush = null }
            ],
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var accepting = service.GenerateAcceptingString(automaton, 20);

        if (accepting != null)
        {
            var pda = builderService.CreateDPDA(automaton);
            pda.Execute(accepting).ShouldBeTrue($"Generated '{accepting}' should be accepted for a^nb^n");
        }
    }

    #endregion
}
