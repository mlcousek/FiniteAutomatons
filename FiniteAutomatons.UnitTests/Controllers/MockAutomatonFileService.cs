using FiniteAutomatons.Services.Interfaces; 
using Microsoft.AspNetCore.Http; 
using FiniteAutomatons.Core.Models.ViewModel; 

namespace FiniteAutomatons.UnitTests.Controllers;

public class MockAutomatonFileService : IAutomatonFileService
{
    public Task<(bool Ok, AutomatonViewModel? Model, string? Error)> LoadFromFileAsync(IFormFile file)
        => Task.FromResult<(bool, AutomatonViewModel?, string?)>((false, null, "Not implemented in mock"));

    public (string FileName, string Content) ExportJson(AutomatonViewModel model)
        => ("test.json", "{}");

    public (string FileName, string Content) ExportText(AutomatonViewModel model)
        => ("test.txt", string.Empty);
}
