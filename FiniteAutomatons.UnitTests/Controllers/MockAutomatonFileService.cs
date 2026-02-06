using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.DTOs;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using System.Text;
using System.Text.Json;

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
    {
        // Create a realistic group export JSON so controller unit tests can validate contents
        var exportData = new GroupExportDto
        {
            GroupName = groupName ?? string.Empty,
            GroupDescription = groupDescription,
            ExportedAt = DateTime.UtcNow,
            Automatons = automatons?.Select(a =>
            {
                AutomatonPayloadDto? content = null;
                SavedExecutionStateDto? exec = null;
                try
                {
                    if (!string.IsNullOrEmpty(a.ContentJson))
                        content = JsonSerializer.Deserialize<AutomatonPayloadDto>(a.ContentJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                }
                catch { content = new AutomatonPayloadDto(); }

                var hasExecutionState = a.SaveMode == AutomatonSaveMode.WithState;
                if (hasExecutionState && !string.IsNullOrEmpty(a.ExecutionStateJson))
                {
                    try
                    {
                        exec = JsonSerializer.Deserialize<SavedExecutionStateDto>(a.ExecutionStateJson, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
                    }
                    catch { }
                }

                return new AutomatonExportItemDto
                {
                    Name = a.Name ?? string.Empty,
                    Description = a.Description,
                    HasExecutionState = hasExecutionState,
                    Content = content ?? new AutomatonPayloadDto(),
                    ExecutionState = exec
                };
            }).ToList() ?? new List<AutomatonExportItemDto>()
        };

        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
        var fileName = $"{groupName}_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        return (fileName, json);
    }

    public async Task<(bool Ok, GroupExportDto? Data, string? Error)> ImportGroupAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return (false, null, "No file uploaded.");

        try
        {
            using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
            var content = await reader.ReadToEndAsync();
            var importData = JsonSerializer.Deserialize<GroupExportDto>(content, new JsonSerializerOptions { PropertyNameCaseInsensitive = true });
            if (importData == null)
                return (false, null, "Invalid group export file.");
            if (importData.Automatons == null)
                return (false, null, "Invalid group export file.");
            if (importData.Automatons.Count == 0)
                return (false, null, "Group export file contains no automatons.");
            return (true, importData, null);
        }
        catch (JsonException)
        {
            // Return a generic failure message so controller presents a consistent user-facing message
            return (false, null, "Failed to import group.");
        }
        catch (Exception ex)
        {
            return (false, null, ex.Message);
        }
    }
}
