using Microsoft.AspNetCore.Http;
using FiniteAutomatons.Core.Models.ViewModel;

namespace FiniteAutomatons.Services.Interfaces;

public interface IAutomatonFileService
{
    Task<(bool Ok, AutomatonViewModel? Model, string? Error)> LoadFromFileAsync(IFormFile file);
    Task<(bool Ok, AutomatonViewModel? Model, string? Error)> LoadViewModelWithStateAsync(IFormFile file);
    (string FileName, string Content) ExportJson(AutomatonViewModel model);
    (string FileName, string Content) ExportJsonWithState(AutomatonViewModel model);
    (string FileName, string Content) ExportText(AutomatonViewModel model);
}
