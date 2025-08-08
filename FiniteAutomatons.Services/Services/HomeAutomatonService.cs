using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.Extensions.Logging;

namespace FiniteAutomatons.Services.Services;

public class HomeAutomatonService(IAutomatonGeneratorService automatonGeneratorService, ILogger<HomeAutomatonService> logger) : IHomeAutomatonService
{
    private readonly IAutomatonGeneratorService automatonGeneratorService = automatonGeneratorService;
    private readonly ILogger<HomeAutomatonService> logger = logger;

    public AutomatonViewModel GenerateDefaultAutomaton()
    {
        logger.LogInformation("Generating random default automaton for home page");
        
        var automatonTypes = new[] { AutomatonType.DFA, AutomatonType.NFA, AutomatonType.EpsilonNFA };
        var random = new Random();
        var selectedType = automatonTypes[random.Next(automatonTypes.Length)];
        
        try
        {
            var transitionCount = selectedType == AutomatonType.DFA ? 
                random.Next(5, 13) :  
                random.Next(6, 15); 
                
            var defaultModel = automatonGeneratorService.GenerateRandomAutomaton(
                selectedType,
                stateCount: 5,
                transitionCount: transitionCount,
                alphabetSize: 4,  
                acceptingStateRatio: 0.3 + random.NextDouble() * 0.2, 
                seed: null 
            );

            defaultModel.IsCustomAutomaton = false;
            
            logger.LogInformation("Generated random default automaton: Type={Type}, States={StateCount}, Transitions={TransitionCount}, Alphabet={AlphabetSize}",
                defaultModel.Type, defaultModel.States?.Count ?? 0, defaultModel.Transitions?.Count ?? 0, defaultModel.Alphabet?.Count ?? 0);

            return defaultModel;
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to generate random default automaton, falling back to simple hardcoded DFA");
            return CreateFallbackAutomaton();
        }
    }

    public AutomatonViewModel CreateFallbackAutomaton()
    {
        logger.LogInformation("Creating fallback automaton for home page");
        
        var fallbackStates = new List<State>
        {
            new() { Id = 1, IsStart = true, IsAccepting = false },
            new() { Id = 2, IsStart = false, IsAccepting = true }
        };
        var fallbackTransitions = new List<Transition>
        {
            new() { FromStateId = 1, ToStateId = 2, Symbol = 'a' },
            new() { FromStateId = 2, ToStateId = 2, Symbol = 'a' }
        };
        var fallbackAlphabet = new List<char> { 'a' };
        
        var fallbackModel = new AutomatonViewModel
        {
            Type = AutomatonType.DFA,
            States = fallbackStates,
            Transitions = fallbackTransitions,
            Alphabet = fallbackAlphabet,
            IsCustomAutomaton = false
        };

        logger.LogInformation("Created fallback automaton model");
        return fallbackModel;
    }
}
