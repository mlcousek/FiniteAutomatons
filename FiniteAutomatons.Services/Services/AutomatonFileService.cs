using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.DoMain;
using FiniteAutomatons.Core.Models.DoMain.FiniteAutomatons;
using FiniteAutomatons.Core.Models.DTOs;
using FiniteAutomatons.Core.Models.Serialization;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Utilities;
using FiniteAutomatons.Services.Interfaces;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using System.Text;
using System.Text.Json;

namespace FiniteAutomatons.Services.Services;

public class AutomatonFileService(ILogger<AutomatonFileService> logger) : IAutomatonFileService
{
    private static readonly JsonSerializerOptions s_indentedSerializerOptions = new() { WriteIndented = true };
    private static readonly JsonSerializerOptions s_caseInsensitiveDeserializerOptions = new() { PropertyNameCaseInsensitive = true };

    private readonly ILogger<AutomatonFileService> logger = logger;

    public async Task<(bool Ok, AutomatonViewModel? Model, string? Error)> LoadFromFileAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return (false, null, "Empty file.");

        string content;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            content = Encoding.UTF8.GetString(ms.ToArray());
        }

        var ext = Path.GetExtension(file.FileName).ToLowerInvariant();
        Automaton? automaton = null;
        var textErrors = new List<string>();
        string? jsonError = null;
        bool ok = false;
        try
        {
            if (ext == ".json")
            {
                ok = AutomatonJsonSerializer.TryDeserialize(content, out automaton, out jsonError);
            }
            else
            {
                ok = AutomatonCustomTextSerializer.TryDeserialize(content, out automaton, out textErrors);
                if (!ok && ext == ".txt")
                {
                    ok = AutomatonJsonSerializer.TryDeserialize(content, out automaton, out jsonError);
                }
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse automaton file {Name}", file.FileName);
            return (false, null, "Failed to parse automaton file.");
        }

        if (!ok || automaton == null)
        {
            var err = jsonError ?? string.Join("; ", textErrors);
            return (false, null, string.IsNullOrWhiteSpace(err) ? "Invalid automaton file." : err);
        }

        var vm = new AutomatonViewModel
        {
            Type = automaton switch
            {
                EpsilonNFA => AutomatonType.EpsilonNFA,
                NFA => AutomatonType.NFA,
                DFA => AutomatonType.DFA,
                PDA => AutomatonType.PDA,
                _ => AutomatonType.DFA
            },
            States = [.. automaton.States.Select(s => new State { Id = s.Id, IsStart = s.IsStart, IsAccepting = s.IsAccepting })],
            Transitions = [.. automaton.Transitions.Select(t => new Transition { FromStateId = t.FromStateId, ToStateId = t.ToStateId, Symbol = t.Symbol, StackPop = t.StackPop, StackPush = t.StackPush })],
            IsCustomAutomaton = true
        };

        vm.NormalizeEpsilonTransitions();
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Loaded automaton from file {Name}: Type={Type} States={States} Transitions={Trans}", file.FileName, vm.Type, vm.States.Count, vm.Transitions.Count);
        }
        return (true, vm, null);
    }

    public async Task<(bool Ok, AutomatonViewModel? Model, string? Error)> LoadViewModelWithStateAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return (false, null, "Empty file.");

        string content;
        using (var ms = new MemoryStream())
        {
            await file.CopyToAsync(ms);
            content = Encoding.UTF8.GetString(ms.ToArray());
        }

        try
        {
            var vm = JsonSerializer.Deserialize<AutomatonViewModel>(content);
            if (vm != null)
            {
                vm.IsCustomAutomaton = true;
                vm.NormalizeEpsilonTransitions();
                return (true, vm, null);
            }
        }
        catch (JsonException ex)
        {
            logger.LogDebug(ex, "Uploaded file is not a full viewmodel JSON, falling back to domain parser.");
        }

        return await LoadFromFileAsync(file);
    }

    public (string FileName, string Content) ExportJson(AutomatonViewModel model)
    {
        var automaton = BuildAutomaton(model);
        var json = AutomatonJsonSerializer.Serialize(automaton);
        var name = $"automaton-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
        return (name, json);
    }

    public (string FileName, string Content) ExportJsonWithState(AutomatonViewModel model)
    {
        model = model ?? throw new ArgumentNullException(nameof(model));
        var content = JsonSerializer.Serialize(model, s_indentedSerializerOptions);
        var name = $"automaton-withstate-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
        return (name, content);
    }

    public (string FileName, string Content) ExportWithInput(AutomatonViewModel model)
    {
        model = model ?? throw new ArgumentNullException(nameof(model));

        var exportModel = new AutomatonViewModel
        {
            Type = model.Type,
            States = model.States,
            Transitions = model.Transitions,
            Input = model.Input,
            IsCustomAutomaton = model.IsCustomAutomaton,
            Position = 0,
            CurrentStateId = null,
            CurrentStates = null,
            IsAccepted = null,
            StateHistorySerialized = string.Empty,
            StackSerialized = null
        };

        var content = JsonSerializer.Serialize(exportModel, s_indentedSerializerOptions);
        var name = $"automaton-withinput-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
        return (name, content);
    }

    public (string FileName, string Content) ExportWithExecutionState(AutomatonViewModel model)
    {
        model = model ?? throw new ArgumentNullException(nameof(model));

        if (string.IsNullOrEmpty(model.Input))
        {
            logger.LogWarning("Exporting automaton with execution state but no input provided");
        }

        var content = JsonSerializer.Serialize(model, s_indentedSerializerOptions);
        var name = $"automaton-execution-{DateTime.UtcNow:yyyyMMddHHmmss}.json";
        return (name, content);
    }

    public (string FileName, string Content) ExportText(AutomatonViewModel model)
    {
        var automaton = BuildAutomaton(model);
        var text = AutomatonCustomTextSerializer.Serialize(automaton);
        var name = $"automaton-{DateTime.UtcNow:yyyyMMddHHmmss}.txt";
        return (name, text);
    }

    public (string FileName, string Content) ExportGroup(string groupName, string? groupDescription, List<SavedAutomaton> automatons)
    {
        ArgumentNullException.ThrowIfNull(groupName);
        ArgumentNullException.ThrowIfNull(automatons);
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Exporting group '{GroupName}' with {Count} automaton(s)", groupName, automatons.Count);
        }
        var exportData = new GroupExportDto
        {
            GroupName = groupName,
            GroupDescription = groupDescription,
            ExportedAt = DateTime.UtcNow,
            Automatons = [.. automatons.Select(a =>
            {
                AutomatonPayloadDto? content = null;
                SavedExecutionStateDto? execState = null;

                try
                {
                    content = JsonSerializer.Deserialize<AutomatonPayloadDto>(a.ContentJson);
                }
                catch (Exception ex)
                {
                    logger.LogWarning(ex, "Failed to deserialize content for automaton {Id}", a.Id);
                    content = new AutomatonPayloadDto();
                }

                if (a.SaveMode >= AutomatonSaveMode.WithInput && !string.IsNullOrEmpty(a.ExecutionStateJson))
                {
                    try
                    {
                        var fullExecState = JsonSerializer.Deserialize<SavedExecutionStateDto>(a.ExecutionStateJson);
                        if (fullExecState != null)
                        {
                            if (a.SaveMode == AutomatonSaveMode.WithInput)
                            {
                                execState = new SavedExecutionStateDto
                                {
                                    Input = fullExecState.Input,
                                    Position = 0,
                                    CurrentStateId = null,
                                    CurrentStates = null,
                                    IsAccepted = null,
                                    StateHistorySerialized = string.Empty,
                                    StackSerialized = null
                                };
                            }
                            else if (a.SaveMode == AutomatonSaveMode.WithState)
                            {
                                execState = fullExecState;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        logger.LogWarning(ex, "Failed to deserialize execution state for automaton {Id}", a.Id);
                    }
                }

                var hasExecutionState = a.SaveMode == AutomatonSaveMode.WithState;

                return new AutomatonExportItemDto
                {
                    Name = a.Name,
                    Description = a.Description,
                    HasExecutionState = hasExecutionState,
                    Content = content ?? new AutomatonPayloadDto(),
                    ExecutionState = execState
                };
            })]
        };

        var json = JsonSerializer.Serialize(exportData, s_indentedSerializerOptions);
        var fileName = $"{SanitizeFileName(groupName)}_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Successfully exported group '{GroupName}' to {FileName}", groupName, fileName);
        }
        return (fileName, json);
    }

    public async Task<(bool Ok, GroupExportDto? Data, string? Error)> ImportGroupAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            logger.LogWarning("ImportGroupAsync called with empty file");
            return (false, null, "No file uploaded.");
        }
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Importing group from file {FileName}", file.FileName);
        }
        try
        {
            string content;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                content = Encoding.UTF8.GetString(ms.ToArray());
            }

            var importData = JsonSerializer.Deserialize<GroupExportDto>(content, s_caseInsensitiveDeserializerOptions);

            if (importData == null)
            {
                logger.LogWarning("Failed to deserialize group import file {FileName}", file.FileName);
                return (false, null, "Invalid group export file format.");
            }

            if (importData.Automatons == null || importData.Automatons.Count == 0)
            {
                logger.LogWarning("Group import file {FileName} contains no automatons", file.FileName);
                return (false, null, "Group export file contains no automatons.");
            }
            if (logger.IsEnabled(LogLevel.Information))
            {
                logger.LogInformation("Successfully imported group '{GroupName}' with {Count} automaton(s)",
                importData.GroupName, importData.Automatons.Count);
            }
            return (true, importData, null);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "JSON parsing error while importing group from {FileName}", file.FileName);
            return (false, null, "Invalid JSON format in group export file.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while importing group from {FileName}", file.FileName);
            return (false, null, "Failed to import group: " + ex.Message);
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
            return "group";

        var invalid = new HashSet<char>(Path.GetInvalidFileNameChars());
        invalid.UnionWith(['<', '>', ':', '"', '/', '\\', '|', '?', '*']);

        var sanitized = new StringBuilder();
        foreach (var c in fileName)
        {
            if (invalid.Contains(c))
                sanitized.Append('_');
            else
                sanitized.Append(c);
        }

        var result = sanitized.ToString().Trim('_');
        return string.IsNullOrWhiteSpace(result) ? "group" : result;
    }

    private static Automaton BuildAutomaton(AutomatonViewModel model)
    {
        Automaton automaton = model.Type switch
        {
            AutomatonType.DFA => new DFA(),
            AutomatonType.NFA => new NFA(),
            AutomatonType.EpsilonNFA => new EpsilonNFA(),
            AutomatonType.PDA => new PDA(),
            _ => new DFA()
        };
        foreach (var s in model.States)
            automaton.AddState(new State { Id = s.Id, IsStart = s.IsStart, IsAccepting = s.IsAccepting });
        foreach (var t in model.Transitions)
            automaton.AddTransition(new Transition { FromStateId = t.FromStateId, ToStateId = t.ToStateId, Symbol = t.Symbol, StackPop = t.StackPop, StackPush = t.StackPush });
        var start = model.States.FirstOrDefault(s => s.IsStart);
        if (start != null)
            automaton.SetStartState(start.Id);
        return automaton;
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

    public void RestoreExecutionStateFromDto(AutomatonViewModel model, SavedExecutionStateDto? executionState, string mode, AutomatonSaveMode saveMode)
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

                    if (saveMode == AutomatonSaveMode.WithState)
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
