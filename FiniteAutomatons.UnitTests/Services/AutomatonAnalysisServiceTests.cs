using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Services.Services;
using Shouldly;

namespace FiniteAutomatons.UnitTests.Services;

public class AutomatonAnalysisServiceTests
{
    [Fact]
    public void GetReachableStates_NoTransitions_ReturnsStartOnly()
    {
        // Arrange
        var svc = new AutomatonAnalysisService();
        var transitions = new List<Transition>();

        // Act
        var reachable = svc.GetReachableStates(transitions, 1);

        // Assert
        reachable.Count.ShouldBe(1);
        reachable.ShouldContain(1);
    }

    [Fact]
    public void GetReachableStates_LinearChain_ReturnsAllInChain()
    {
        // Arrange
        var svc = new AutomatonAnalysisService();
        var transitions = new List<Transition>
        {
            new() { FromStateId = 1, ToStateId = 2 },
            new() { FromStateId = 2, ToStateId = 3 }
        };

        // Act
        var reachable = svc.GetReachableStates(transitions, 1);

        // Assert
        reachable.Count.ShouldBe(3);
        reachable.ShouldContain(1);
        reachable.ShouldContain(2);
        reachable.ShouldContain(3);
    }

    [Fact]
    public void GetReachableStates_WithCycle_DoesNotLoopInfinitely()
    {
        // Arrange
        var svc = new AutomatonAnalysisService();
        var transitions = new List<Transition>
        {
            new() { FromStateId = 1, ToStateId = 2 },
            new() { FromStateId = 2, ToStateId = 1 },
            new() { FromStateId = 1, ToStateId = 3 }
        };

        // Act
        var reachable = svc.GetReachableStates(transitions, 1);

        // Assert
        reachable.Count.ShouldBe(3);
        reachable.ShouldContain(1);
        reachable.ShouldContain(2);
        reachable.ShouldContain(3);
    }

    [Fact]
    public void GetReachableStates_DisconnectedGraph_ReturnsComponentOnly()
    {
        // Arrange
        var svc = new AutomatonAnalysisService();
        var transitions = new List<Transition>
        {
            new() { FromStateId = 1, ToStateId = 2 },
            new() { FromStateId = 3, ToStateId = 4 }
        };

        // Act
        var reachableFrom1 = svc.GetReachableStates(transitions, 1);
        var reachableFrom3 = svc.GetReachableStates(transitions, 3);

        // Assert
        reachableFrom1.Count.ShouldBe(2);
        reachableFrom1.ShouldContain(1);
        reachableFrom1.ShouldContain(2);

        reachableFrom3.Count.ShouldBe(2);
        reachableFrom3.ShouldContain(3);
        reachableFrom3.ShouldContain(4);
    }

    [Fact]
    public void GetReachableCount_ConvenienceMethod_ReturnsCorrectCount()
    {
        // Arrange
        var svc = new AutomatonAnalysisService();
        var transitions = new List<Transition>
        {
            new() { FromStateId = 1, ToStateId = 2 },
            new() { FromStateId = 2, ToStateId = 3 },
            new() { FromStateId = 2, ToStateId = 4 }
        };

        // Act
        var count = svc.GetReachableCount(transitions, 1);

        // Assert
        count.ShouldBe(4); // 1,2,3,4
    }

    [Fact]
    public void GetReachableStates_StartNotInTransitions_ReturnsStartOnly()
    {
        // Arrange
        var svc = new AutomatonAnalysisService();
        var transitions = new List<Transition>
        {
            new() { FromStateId = 2, ToStateId = 3 }
        };

        // Act
        var reachable = svc.GetReachableStates(transitions, 99);

        // Assert
        reachable.Count.ShouldBe(1);
        reachable.ShouldContain(99);
    }

    [Fact]
    public void GetReachableStates_NullTransitions_Throws()
    {
        // Arrange
        var svc = new AutomatonAnalysisService();

        // Act & Assert
        Should.Throw<NullReferenceException>(() => svc.GetReachableStates(null!, 1));
    }
}
