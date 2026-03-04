using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.DTOs;
using FiniteAutomatons.Core.Models.ViewModel;
using Microsoft.AspNetCore.Http;

namespace FiniteAutomatons.Services.Interfaces;

public interface IAutomatonFileService
{
    Task<(bool Ok, AutomatonViewModel? Model, string? Error)> LoadFromFileAsync(IFormFile file);
    Task<(bool Ok, AutomatonViewModel? Model, string? Error)> LoadViewModelWithStateAsync(IFormFile file);
    (string FileName, string Content) ExportJson(AutomatonViewModel model);
    (string FileName, string Content) ExportJsonWithState(AutomatonViewModel model);
    (string FileName, string Content) ExportText(AutomatonViewModel model);
    (string FileName, string Content) ExportWithInput(AutomatonViewModel model);
    (string FileName, string Content) ExportWithExecutionState(AutomatonViewModel model);
    (string FileName, string Content) ExportGroup(string groupName, string? groupDescription, List<SavedAutomaton> automatons);
    Task<(bool Ok, GroupExportDto? Data, string? Error)> ImportGroupAsync(IFormFile file);
    void RestoreExecutionState(AutomatonViewModel model, string? executionStateJson, string mode);
    void RestoreExecutionStateFromDto(AutomatonViewModel model, SavedExecutionStateDto? executionState, string mode, AutomatonSaveMode saveMode);
}
