using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace FiniteAutomatons.UnitTests.Controllers;

public class MockAutomatonTempDataService : IAutomatonTempDataService
{
    public (bool Success, AutomatonViewModel? Model) TryGetCustomAutomaton(ITempDataDictionary tempData)
    {
        return (false, null);
    }

    public void StoreCustomAutomaton(ITempDataDictionary tempData, AutomatonViewModel model)
    {
        var modelJson = System.Text.Json.JsonSerializer.Serialize(model);
        tempData["CustomAutomaton"] = modelJson;
    }

    public void StoreErrorMessage(ITempDataDictionary tempData, string errorMessage)
    {
        tempData["ErrorMessage"] = errorMessage;
    }

    public void StoreConversionMessage(ITempDataDictionary tempData, string message)
    {
        tempData["ConversionMessage"] = message;
    }

    public (bool Success, AutomatonViewModel? Model) TryGetSessionAutomaton(ISession session, string sessionKey)
    {
        return (false, null);
    }
}
