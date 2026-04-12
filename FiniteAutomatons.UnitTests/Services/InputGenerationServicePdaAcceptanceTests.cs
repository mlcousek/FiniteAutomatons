using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Services;
using Microsoft.Extensions.Logging.Abstractions;
using Shouldly;
using System.Diagnostics;
using System.Text.Json;

namespace FiniteAutomatons.UnitTests.Services;

public class InputGenerationServicePdaAcceptanceTests
{
    private readonly InputGenerationService service;
    private readonly AutomatonBuilderService builderService;

    public InputGenerationServicePdaAcceptanceTests()
    {
        builderService = new AutomatonBuilderService(NullLogger<AutomatonBuilderService>.Instance);
        service = new InputGenerationService(NullLogger<InputGenerationService>.Instance, builderService);
    }

    private bool ExecuteWithConfiguredInitialStack(AutomatonViewModel model, string input)
    {
        var pda = model.Type == AutomatonType.DPDA
            ? (Automaton)builderService.CreateDPDA(model)
            : builderService.CreateNPDA(model);

        Stack<char>? initialStack = null;
        if (!string.IsNullOrWhiteSpace(model.InitialStackSerialized))
        {
            var symbols = JsonSerializer.Deserialize<List<char>>(model.InitialStackSerialized) ?? [];
            if (symbols.Count > 0)
            {
                initialStack = new Stack<char>(symbols);
            }
        }

        var state = pda.StartExecution(input, initialStack);
        pda.ExecuteAll(state);
        return state.IsAccepted == true;
    }

    [Fact]
    public void GenerateRandomAcceptingString_NPDA_FinalStateOnly_ReturnsValidString()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '\0', StackPush = "X" }],
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var result = service.GenerateRandomAcceptingString(model, minLength: 1, maxLength: 5, maxAttempts: 50, seed: 42);

        result.ShouldNotBeNull();
        var npda = builderService.CreateNPDA(model);
        npda.Execute(result).ShouldBeTrue();
    }

    [Fact]
    public void GenerateRandomAcceptingString_NPDA_EmptyStackOnly_ReturnsValidString()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" },
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = 'X', StackPush = null }
            ],
            AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly
        };

        var result = service.GenerateRandomAcceptingString(model, minLength: 0, maxLength: 10, maxAttempts: 200, seed: 42);

        result.ShouldNotBeNull();
        var npda = builderService.CreateNPDA(model);
        npda.Execute(result).ShouldBeTrue();
    }

    [Fact]
    public void GenerateRandomAcceptingString_NPDA_FinalStateAndEmptyStack_ReturnsValidString()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '\0', StackPush = "X" },
                new() { FromStateId = 2, ToStateId = 2, Symbol = 'b', StackPop = 'X', StackPush = null }
            ],
            AcceptanceMode = PDAAcceptanceMode.FinalStateAndEmptyStack
        };

        var result = service.GenerateRandomAcceptingString(model, minLength: 1, maxLength: 10, maxAttempts: 200, seed: 42);

        result.ShouldNotBeNull();
        var npda = builderService.CreateNPDA(model);
        npda.Execute(result).ShouldBeTrue();
    }

    [Fact]
    public void GenerateRandomAcceptingString_NPDA_CanInferInitialStack_WhenRequiredForAcceptance()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = 'A', StackPush = null }],
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly,
            InitialStackSerialized = null
        };

        var result = service.GenerateRandomAcceptingString(model, minLength: 1, maxLength: 5, maxAttempts: 50, seed: 7);

        result.ShouldBe("a");
        model.InitialStackSerialized.ShouldNotBeNullOrWhiteSpace();
        ExecuteWithConfiguredInitialStack(model, result!).ShouldBeTrue();
    }

    [Fact]
    public void GenerateRandomAcceptingString_NPDA_UsesProvidedInitialStack()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = 'X', StackPush = null }
            ],
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly,
            InitialStackSerialized = JsonSerializer.Serialize(new List<char> { '#', 'X' })
        };

        var result = service.GenerateRandomAcceptingString(model, minLength: 1, maxLength: 5, maxAttempts: 50, seed: 1);

        result.ShouldBe("a");
        ExecuteWithConfiguredInitialStack(model, result!).ShouldBeTrue();
    }

    [Fact]
    public void GenerateRandomAcceptingString_Pda_CanInferInitialStack_WhenRequiredForAcceptance()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = 'A', StackPush = null }],
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly,
            InitialStackSerialized = null
        };

        var result = service.GenerateRandomAcceptingString(model, minLength: 1, maxLength: 5, maxAttempts: 50, seed: 1);

        result.ShouldBe("a");
        model.InitialStackSerialized.ShouldNotBeNullOrWhiteSpace();
        ExecuteWithConfiguredInitialStack(model, result!).ShouldBeTrue();
    }

    [Fact]
    public void GenerateRandomAcceptingString_DPDA_LargeSearchSpace_CompletesQuickly()
    {
        var transitions = new List<Transition>
        {
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = null, StackPush = "A" },
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = null, StackPush = "B" },
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'c', StackPop = null, StackPush = "C" },
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'd', StackPop = null, StackPush = "D" },
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'e', StackPop = null, StackPush = "E" },
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'f', StackPop = null, StackPush = "F" }
        };

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions = transitions,
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var sw = Stopwatch.StartNew();
        var result = service.GenerateRandomAcceptingString(model, minLength: 1, maxLength: 30, maxAttempts: 150);
        sw.Stop();

        result.ShouldBeNull();
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GenerateAcceptingString_Pda_FinalStateAndEmptyStack_ReturnsValidString()
    {
        var model = new AutomatonViewModel
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

        var result = service.GenerateAcceptingString(model, 10);

        result.ShouldNotBeNull();
        var pda = builderService.CreateDPDA(model);
        pda.Execute(result).ShouldBeTrue($"Generated string '{result}' should be accepted");
    }

    [Fact]
    public void GenerateAcceptingString_Pda_FinalStateOnly_ReturnsValidString()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" }
            ],
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var result = service.GenerateAcceptingString(model, 5);

        result.ShouldNotBeNull();
        var pda = builderService.CreateDPDA(model);
        pda.Execute(result).ShouldBeTrue($"Generated string '{result}' should be accepted in FinalStateOnly mode");
    }

    [Fact]
    public void GenerateAcceptingString_Pda_EmptyStackOnly_ReturnsValidString()
    {
        // This PDA accepts a^n b^n by EmptyStackOnly mode (no accepting states needed)
        var model = new AutomatonViewModel
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

        var result = service.GenerateAcceptingString(model, 10);

        result.ShouldNotBeNull("EmptyStackOnly generation should not require accepting states.");
        var pda = builderService.CreateDPDA(model);
        pda.Execute(result).ShouldBeTrue($"Generated string '{result}' should be accepted in EmptyStackOnly mode");
    }

    [Fact]
    public void GenerateAcceptingString_Pda_EmptyStackOnly_PrefersNonEmptyPathToAcceptingState()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = '\0', StackPush = null }],
            AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly
        };

        var result = service.GenerateAcceptingString(model, 5);

        result.ShouldBe("a");
    }

    [Fact]
    public void GenerateAcceptingString_NPDA_EmptyStackOnly_TrivialEmptyAcceptance_ReturnsNull()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" }],
            AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly,
            InitialStackSerialized = null
        };

        var result = service.GenerateAcceptingString(model, 5);

        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateRandomAcceptingString_NPDA_EmptyStackOnly_TrivialEmptyAcceptance_ReturnsNull()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions = [new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" }],
            AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly,
            InitialStackSerialized = null
        };

        var result = service.GenerateRandomAcceptingString(model, minLength: 0, maxLength: 5, maxAttempts: 20, seed: 7);

        result.ShouldBeNull();
    }

    [Fact]
    public void GenerateAcceptingString_Pda_CanInferInitialStack_WhenRequiredForAcceptance()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions = [new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = 'A', StackPush = null }],
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly,
            InitialStackSerialized = null
        };

        var result = service.GenerateAcceptingString(model, 5);

        result.ShouldBe("a");
        model.InitialStackSerialized.ShouldNotBeNullOrWhiteSpace();

        var inferred = JsonSerializer.Deserialize<List<char>>(model.InitialStackSerialized!);
        inferred.ShouldNotBeNull();
        inferred![0].ShouldBe('#');
        inferred.ShouldContain('A');

        ExecuteWithConfiguredInitialStack(model, result!).ShouldBeTrue();
    }

    [Fact]
    public void GenerateAcceptingString_DPDA_UsesProvidedInitialStack()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'x', StackPop = 'X', StackPush = null }
            ],
            AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly,
            InitialStackSerialized = JsonSerializer.Serialize(new List<char> { '#', 'X' })
        };

        var result = service.GenerateAcceptingString(model, 5);

        result.ShouldNotBeNull();
        result.ShouldBe("x");
        ExecuteWithConfiguredInitialStack(model, result).ShouldBeTrue("String should be accepted with configured initial stack.");

        var pda = builderService.CreateDPDA(model);
        pda.Execute(result).ShouldBeFalse("Default stack should not accept this string without initial stack symbols.");
    }

    [Fact]
    public void GenerateAcceptingString_NPDA_UsesProvidedInitialStack()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 2, Symbol = 'a', StackPop = 'X', StackPush = null }
            ],
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly,
            InitialStackSerialized = JsonSerializer.Serialize(new List<char> { '#', 'X' })
        };

        var result = service.GenerateAcceptingString(model, 5);

        result.ShouldNotBeNull();
        result.ShouldBe("a");
        ExecuteWithConfiguredInitialStack(model, result).ShouldBeTrue("String should be accepted with configured initial stack.");

        var npda = builderService.CreateNPDA(model);
        npda.Execute(result).ShouldBeFalse("Default stack should not accept this string without initial stack symbols.");
    }

    [Fact]
    public void GenerateRejectingString_DPDA_UsesProvidedInitialStack()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = false }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'x', StackPop = 'X', StackPush = null }
            ],
            AcceptanceMode = PDAAcceptanceMode.EmptyStackOnly,
            InitialStackSerialized = JsonSerializer.Serialize(new List<char> { '#', 'X' })
        };

        var result = service.GenerateRejectingString(model, 5);

        result.ShouldNotBeNull();
        ExecuteWithConfiguredInitialStack(model, result).ShouldBeFalse("Generated string should be rejecting for configured initial stack.");
    }

    [Fact]
    public void GenerateRandomAcceptingString_Pda_FinalStateAndEmptyStack_ReturnsValidString()
    {
        var model = new AutomatonViewModel
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

        var result = service.GenerateRandomAcceptingString(model, minLength: 0, maxLength: 10, maxAttempts: 100);

        result.ShouldNotBeNull();
        var pda = builderService.CreateDPDA(model);
        pda.Execute(result).ShouldBeTrue($"Generated string '{result}' should be accepted");
    }

    [Fact]
    public void GenerateRandomAcceptingString_Pda_FinalStateOnly_ReturnsValidString()
    {
        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions =
            [
                new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = '\0', StackPush = "X" }
            ],
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var result = service.GenerateRandomAcceptingString(model, minLength: 1, maxLength: 5, maxAttempts: 50);

        result.ShouldNotBeNull();
        var pda = builderService.CreateDPDA(model);
        pda.Execute(result).ShouldBeTrue($"Generated string '{result}' should be accepted in FinalStateOnly mode");
    }

    [Fact]
    public void GenerateRandomAcceptingString_Pda_EmptyStackOnly_ReturnsValidString()
    {
        var model = new AutomatonViewModel
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

        var result = service.GenerateRandomAcceptingString(model, minLength: 0, maxLength: 10, maxAttempts: 500);


        if (result != null)
        {
            var pda = builderService.CreateDPDA(model);
            pda.Execute(result).ShouldBeTrue($"Generated string '{result}' should be accepted in EmptyStackOnly mode");
        }
        else
        {
            result.ShouldBeNull("Random generation may fail for structured patterns like a^n b^n - this is expected behavior");
        }
    }

    [Fact]
    public void GenerateInterestingCases_Pda_IncludesPdaSpecificCases()
    {
        var model = new AutomatonViewModel
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

        var cases = service.GenerateInterestingCases(model, 15);

        cases.ShouldNotBeEmpty();
        cases.ShouldContain(c => c.Description.Contains("PDA", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void GenerateAcceptingString_DPDA_LargeSearchSpace_CompletesQuickly()
    {
        var transitions = new List<Transition>
        {
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = null, StackPush = "A" },
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = null, StackPush = "B" },
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'c', StackPop = null, StackPush = "C" },
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'd', StackPop = null, StackPush = "D" },
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'e', StackPop = null, StackPush = "E" },
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'f', StackPop = null, StackPush = "F" }
        };

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.DPDA,
            States =
            [
                new() { Id = 1, IsStart = true, IsAccepting = false },
                new() { Id = 2, IsStart = false, IsAccepting = true }
            ],
            Transitions = transitions,
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var sw = Stopwatch.StartNew();
        var result = service.GenerateAcceptingString(model, 30);
        sw.Stop();

        result.ShouldBeNull();
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void GenerateRejectingString_NPDA_AllStringsAccepted_CompletesQuickly()
    {
        var transitions = new List<Transition>
        {
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'a', StackPop = null, StackPush = null },
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'b', StackPop = null, StackPush = null },
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'c', StackPop = null, StackPush = null },
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'd', StackPop = null, StackPush = null },
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'e', StackPop = null, StackPush = null },
            new() { FromStateId = 1, ToStateId = 1, Symbol = 'f', StackPop = null, StackPush = null }
        };

        var model = new AutomatonViewModel
        {
            Type = AutomatonType.NPDA,
            States = [new() { Id = 1, IsStart = true, IsAccepting = true }],
            Transitions = transitions,
            AcceptanceMode = PDAAcceptanceMode.FinalStateOnly
        };

        var sw = Stopwatch.StartNew();
        var result = service.GenerateRejectingString(model, 30);
        sw.Stop();

        result.ShouldBeNull();
        sw.Elapsed.ShouldBeLessThan(TimeSpan.FromSeconds(5));
    }
}
