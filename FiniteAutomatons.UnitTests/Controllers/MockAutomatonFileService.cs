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
    private static readonly JsonSerializerOptions s_indentedOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions s_caseInsensitiveOptions = new() { PropertyNameCaseInsensitive = true };

    public Task<(bool Ok, AutomatonViewModel? Model, string? Error)> LoadFromFileAsync(IFormFile file)
        => Task.FromResult<(bool, AutomatonViewModel?, string?)>((false, null, "Not implemented in mock"));

    public async Task<(bool Ok, AutomatonViewModel? Model, string? Error)> LoadViewModelWithStateAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return (false, null, "No file uploaded.");

        using var reader = new StreamReader(file.OpenReadStream(), Encoding.UTF8);
        var content = await reader.ReadToEndAsync();

        if (string.IsNullOrWhiteSpace(content))
            return (false, null, "No file uploaded.");

        JsonDocument? doc = null;
        try
        {
            doc = JsonDocument.Parse(content);
        }
        catch (JsonException)
        {
            return (false, null, "Failed to import group.");
        }

        using (doc)
        {
            if (doc.RootElement.ValueKind == JsonValueKind.Object &&
                doc.RootElement.TryGetProperty("Automatons", out _))
            {
                return (false, null, "Invalid group export file.");
            }
        }

        try
        {
            var vm = JsonSerializer.Deserialize<AutomatonViewModel>(content, s_caseInsensitiveOptions);
            if (vm == null)
                return (false, null, "Failed to import group.");

            vm.IsCustomAutomaton = true;
            return (true, vm, null);
        }
        catch (JsonException)
        {
            return (false, null, "Failed to import group.");
        }
    }

    public (string FileName, string Content) ExportJson(AutomatonViewModel model)
    {
        var json = JsonSerializer.Serialize(model, s_indentedOptions);
        return ("test.json", json);
    }

    public (string FileName, string Content) ExportText(AutomatonViewModel model)
        => ("test.txt", string.Empty);

    public (string FileName, string Content) ExportJsonWithState(AutomatonViewModel model)
    {
        var json = JsonSerializer.Serialize(model, s_indentedOptions);
        return ("test-withstate.json", json);
    }

    public (string FileName, string Content) ExportWithInput(AutomatonViewModel model)
    {
        var json = JsonSerializer.Serialize(model, s_indentedOptions);
        return ("test-withinput.json", json);
    }

    public (string FileName, string Content) ExportWithExecutionState(AutomatonViewModel model)
    {
        var json = JsonSerializer.Serialize(model, s_indentedOptions);
        return ("test-execution.json", json);
    }

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
                        content = JsonSerializer.Deserialize<AutomatonPayloadDto>(a.ContentJson, s_caseInsensitiveOptions);
                }
                catch { content = new AutomatonPayloadDto(); }

                var hasExecutionState = a.SaveMode == AutomatonSaveMode.WithState;
                if (hasExecutionState && !string.IsNullOrEmpty(a.ExecutionStateJson))
                {
                    try
                    {
                        exec = JsonSerializer.Deserialize<SavedExecutionStateDto>(a.ExecutionStateJson, s_caseInsensitiveOptions);
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
            }).ToList() ?? []
        };

        var json = JsonSerializer.Serialize(exportData, s_indentedOptions);
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
            var importData = JsonSerializer.Deserialize<GroupExportDto>(content, s_caseInsensitiveOptions);
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

    public void RestoreExecutionState(AutomatonViewModel model, string? executionStateJson, string mode)
    {
        ArgumentNullException.ThrowIfNull(model);

        switch (mode.ToLowerInvariant())
        {
            case "input":
                if (!string.IsNullOrEmpty(executionStateJson))
                {
                    var execState = JsonSerializer.Deserialize<JsonElement>(executionStateJson);
                    if (execState.ValueKind != JsonValueKind.Undefined && execState.TryGetProperty("Input", out var input))
                    {
                        model.Input = input.GetString() ?? string.Empty;
                    }
                }
                model.Position = 0;
                model.CurrentStateId = null;
                model.CurrentStates = null;
                model.IsAccepted = null;
                model.StateHistorySerialized = string.Empty;
                model.StackSerialized = null;
                break;

            case "state":
                if (!string.IsNullOrEmpty(executionStateJson))
                {
                    var execState = JsonSerializer.Deserialize<JsonElement>(executionStateJson);
                    if (execState.ValueKind != JsonValueKind.Undefined)
                    {
                        if (execState.TryGetProperty("Input", out var input)) model.Input = input.GetString() ?? string.Empty;
                        if (execState.TryGetProperty("Position", out var pos)) model.Position = pos.GetInt32();
                        if (execState.TryGetProperty("CurrentStateId", out var csid) && csid.ValueKind != JsonValueKind.Null)
                            model.CurrentStateId = csid.GetInt32();
                        if (execState.TryGetProperty("IsAccepted", out var acc) && acc.ValueKind != JsonValueKind.Null)
                            model.IsAccepted = acc.GetBoolean();
                        if (execState.TryGetProperty("StateHistorySerialized", out var hist))
                            model.StateHistorySerialized = hist.GetString() ?? string.Empty;
                        if (execState.TryGetProperty("StackSerialized", out var stack) && stack.ValueKind != JsonValueKind.Null)
                            model.StackSerialized = stack.GetString();
                    }
                }
                break;

            default:
                model.Input = string.Empty;
                model.Position = 0;
                model.CurrentStateId = null;
                model.CurrentStates = null;
                model.IsAccepted = null;
                model.StateHistorySerialized = string.Empty;
                model.StackSerialized = null;
                break;
        }
    }

    public void RestoreExecutionStateFromDto(AutomatonViewModel model, Core.Models.DTOs.SavedExecutionStateDto? executionState, string mode, Core.Models.Database.AutomatonSaveMode saveMode)
    {
        ArgumentNullException.ThrowIfNull(model);

        switch (mode.ToLowerInvariant())
        {
            case "input":
                if (executionState != null)
                {
                    model.Input = executionState.Input ?? string.Empty;
                }
                model.Position = 0;
                model.CurrentStateId = null;
                model.CurrentStates = null;
                model.IsAccepted = null;
                model.StateHistorySerialized = string.Empty;
                model.StackSerialized = null;
                model.HasExecuted = false;
                break;

            case "state":
                if (executionState != null)
                {
                    model.Input = executionState.Input ?? string.Empty;

                    if (saveMode == Core.Models.Database.AutomatonSaveMode.WithState)
                    {
                        model.Position = executionState.Position;
                        model.CurrentStateId = executionState.CurrentStateId;
                        model.CurrentStates = executionState.CurrentStates != null ? [.. executionState.CurrentStates] : null;
                        model.IsAccepted = executionState.IsAccepted;
                        model.StateHistorySerialized = executionState.StateHistorySerialized ?? string.Empty;
                        model.StackSerialized = executionState.StackSerialized;
                        model.HasExecuted = true;
                    }
                }
                break;

            default:
                model.Input = string.Empty;
                model.Position = 0;
                model.CurrentStateId = null;
                model.CurrentStates = null;
                model.IsAccepted = null;
                model.StateHistorySerialized = string.Empty;
                model.StackSerialized = null;
                model.HasExecuted = false;
                break;
        }
    }
}
