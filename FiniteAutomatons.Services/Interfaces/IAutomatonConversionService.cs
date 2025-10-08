using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

public interface IAutomatonConversionService
{

    (AutomatonViewModel ConvertedModel, List<string> Warnings) ConvertAutomatonType(AutomatonViewModel model, AutomatonType newType);

    AutomatonViewModel ConvertToDFA(AutomatonViewModel model);
}
