using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace FiniteAutomatons.Services.Services;

public class AutomatonTempDataService(ILogger<AutomatonTempDataService> logger) : IAutomatonTempDataService
{
    private readonly ILogger<AutomatonTempDataService> logger = logger;

    public (bool Success, AutomatonViewModel? Model) TryGetCustomAutomaton(ITempDataDictionary tempData)
    {
        if (tempData["CustomAutomaton"] == null)
        {
            logger.LogInformation("No CustomAutomaton found in TempData");
            return (false, null);
        }

        var modelJson = tempData["CustomAutomaton"] as string;
        logger.LogInformation("Found CustomAutomaton in TempData, length: {Length}", modelJson?.Length ?? 0);

        if (string.IsNullOrEmpty(modelJson))
        {
            logger.LogWarning("CustomAutomaton TempData is null or empty");
            return (false, null);
        }

        try
        {
            var customModel = JsonSerializer.Deserialize<AutomatonViewModel>(modelJson);
            if (customModel == null)
            {
                logger.LogWarning("Deserialized AutomatonViewModel is null");
                return (false, null);
            }

            logger.LogInformation("Successfully deserialized custom automaton: Type={Type}, States={StateCount}, IsCustom={IsCustom}",
                customModel.Type, customModel.States?.Count ?? 0, customModel.IsCustomAutomaton);

            customModel.IsCustomAutomaton = true;
            
            customModel.States ??= [];
            customModel.Transitions ??= [];
            customModel.Alphabet ??= [];
            customModel.Input ??= "";

            return (true, customModel);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to deserialize custom automaton from TempData");
            StoreErrorMessage(tempData, "Failed to load custom automaton.");
            return (false, null);
        }
    }

    public void StoreCustomAutomaton(ITempDataDictionary tempData, AutomatonViewModel model)
    {
        try
        {
            var modelJson = JsonSerializer.Serialize(model);
            tempData["CustomAutomaton"] = modelJson;
            logger.LogInformation("Successfully stored custom automaton in TempData: Type={Type}, States={StateCount}",
                model.Type, model.States?.Count ?? 0);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "Failed to serialize automaton for TempData storage");
            throw new InvalidOperationException("Failed to store automaton in session data", ex);
        }
    }

    public void StoreErrorMessage(ITempDataDictionary tempData, string errorMessage)
    {
        tempData["ErrorMessage"] = errorMessage;
        logger.LogInformation("Stored error message in TempData: {Message}", errorMessage);
    }

    public void StoreConversionMessage(ITempDataDictionary tempData, string message)
    {
        tempData["ConversionMessage"] = message;
        logger.LogInformation("Stored conversion message in TempData: {Message}", message);
    }
}
