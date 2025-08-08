using FiniteAutomatons.Core.Models.ViewModel;
using Microsoft.AspNetCore.Mvc.ViewFeatures;

namespace FiniteAutomatons.Services.Interfaces;

public interface IAutomatonTempDataService
{
    (bool Success, AutomatonViewModel? Model) TryGetCustomAutomaton(ITempDataDictionary tempData);

    void StoreCustomAutomaton(ITempDataDictionary tempData, AutomatonViewModel model);

    void StoreErrorMessage(ITempDataDictionary tempData, string errorMessage);

    void StoreConversionMessage(ITempDataDictionary tempData, string message);
}
