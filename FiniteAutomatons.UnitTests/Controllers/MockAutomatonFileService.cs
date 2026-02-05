using FiniteAutomatons.Services.Interfaces; 
using Microsoft.AspNetCore.Http; 
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Models.DTOs;
using FiniteAutomatons.Core.Models.Database;

namespace FiniteAutomatons.UnitTests.Controllers;

public class MockAutomatonFileService : IAutomatonFileService
{
    public Task<(bool Ok, AutomatonViewModel? Model, string? Error)> LoadFromFileAsync(IFormFile file)
        => Task.FromResult<(bool, AutomatonViewModel?, string?)>((false, null, "Not implemented in mock"));

    public Task<(bool Ok, AutomatonViewModel? Model, string? Error)> LoadViewModelWithStateAsync(IFormFile file)
        => Task.FromResult<(bool, AutomatonViewModel?, string?)>((false, null, "Not implemented in mock"));

    public (string FileName, string Content) ExportJson(AutomatonViewModel model)
        => ("test.json", "{}");

    public (string FileName, string Content) ExportText(AutomatonViewModel model)
        => ("test.txt", string.Empty);

    public (string FileName, string Content) ExportJsonWithState(AutomatonViewModel model)
        => ("test-withstate.json", "{}");

    public (string FileName, string Content) ExportWithInput(AutomatonViewModel model)
        => ("test-withinput.json", "{}");

    public (string FileName, string Content) ExportWithExecutionState(AutomatonViewModel model)
        => ("test-execution.json", "{}");

    public (string FileName, string Content) ExportGroup(string groupName, string? groupDescription, List<SavedAutomaton> automatons)
        => ($"{groupName}_export.json", "{}");

    public Task<(bool Ok, GroupExportDto? Data, string? Error)> ImportGroupAsync(IFormFile file)
        => Task.FromResult<(bool, GroupExportDto?, string?)>((false, null, "Not implemented in mock"));
}
