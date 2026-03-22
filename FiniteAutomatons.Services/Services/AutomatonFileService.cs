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

        var content = await ReadFileContentAsync(file);
        var parseResult = ParseAutomatonFile(content, file.FileName);

        if (!parseResult.IsSuccess)
            return (false, null, parseResult.ErrorMessage);

        var viewModel = CreateViewModelFromAutomaton(parseResult.Automaton!);
        LogSuccessfulLoad(file.FileName, viewModel);

        return (true, viewModel, null);
    }

    private static async Task<string> ReadFileContentAsync(IFormFile file)
    {
        using var ms = new MemoryStream();
        await file.CopyToAsync(ms);
        return Encoding.UTF8.GetString(ms.ToArray());
    }

    private AutomatonParseResult ParseAutomatonFile(string content, string fileName)
    {
        var ext = Path.GetExtension(fileName).ToLowerInvariant();

        try
        {
            if (ext == ".json")
            {
                return TryParseJson(content);
            }

            return TryParseTextFormat(content, ext);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to parse automaton file {Name}", fileName);
            return AutomatonParseResult.Failure("Failed to parse automaton file.");
        }
    }

    private static AutomatonParseResult TryParseJson(string content)
    {
        var ok = AutomatonJsonSerializer.TryDeserialize(content, out var automaton, out var jsonError);
        return ok && automaton != null
            ? AutomatonParseResult.Success(automaton)
            : AutomatonParseResult.Failure(string.IsNullOrWhiteSpace(jsonError) ? "Invalid automaton file." : jsonError);
    }

    private static AutomatonParseResult TryParseTextFormat(string content, string extension)
    {
        var ok = AutomatonCustomTextSerializer.TryDeserialize(content, out var automaton, out var textErrors);

        if (!ok && extension == ".txt")
        {
            ok = AutomatonJsonSerializer.TryDeserialize(content, out automaton, out var jsonError);
            if (ok && automaton != null)
                return AutomatonParseResult.Success(automaton);

            return AutomatonParseResult.Failure(string.IsNullOrWhiteSpace(jsonError) ? "Invalid automaton file." : jsonError);
        }

        if (ok && automaton != null)
            return AutomatonParseResult.Success(automaton);

        var errorMessage = string.Join("; ", textErrors);
        return AutomatonParseResult.Failure(string.IsNullOrWhiteSpace(errorMessage) ? "Invalid automaton file." : errorMessage);
    }

    private static AutomatonViewModel CreateViewModelFromAutomaton(Automaton automaton)
    {
        var vm = new AutomatonViewModel
        {
            Type = DetermineAutomatonType(automaton),
            States = [.. automaton.States.Select(s => new State { Id = s.Id, IsStart = s.IsStart, IsAccepting = s.IsAccepting })],
            Transitions = [.. automaton.Transitions.Select(t => new Transition
            {
                FromStateId = t.FromStateId,
                ToStateId = t.ToStateId,
                Symbol = t.Symbol,
                StackPop = t.StackPop,
                StackPush = t.StackPush
            })],
            IsCustomAutomaton = true
        };

        vm.NormalizeEpsilonTransitions();
        return vm;
    }

    private static AutomatonType DetermineAutomatonType(Automaton automaton)
    {
        return automaton switch
        {
            EpsilonNFA => AutomatonType.EpsilonNFA,
            NFA => AutomatonType.NFA,
            DFA => AutomatonType.DFA,
            DPDA => AutomatonType.DPDA,
            NPDA => AutomatonType.NPDA,
            _ => AutomatonType.DFA
        };
    }

    private void LogSuccessfulLoad(string fileName, AutomatonViewModel viewModel)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation(
                "Loaded automaton from file {Name}: Type={Type} States={States} Transitions={Trans}",
                fileName, viewModel.Type, viewModel.States.Count, viewModel.Transitions.Count);
        }
    }

    private class AutomatonParseResult
    {
        public bool IsSuccess { get; init; }
        public Automaton? Automaton { get; init; }
        public string? ErrorMessage { get; init; }

        public static AutomatonParseResult Success(Automaton automaton) =>
            new() { IsSuccess = true, Automaton = automaton };

        public static AutomatonParseResult Failure(string errorMessage) =>
            new() { IsSuccess = false, ErrorMessage = errorMessage };
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

        LogExportStart(groupName, automatons.Count);

        var exportData = CreateGroupExportData(groupName, groupDescription, automatons);
        var json = JsonSerializer.Serialize(exportData, s_indentedSerializerOptions);
        var fileName = GenerateExportFileName(groupName);

        LogExportSuccess(groupName, fileName);

        return (fileName, json);
    }

    private GroupExportDto CreateGroupExportData(string groupName, string? groupDescription, List<SavedAutomaton> automatons)
    {
        return new GroupExportDto
        {
            GroupName = groupName,
            GroupDescription = groupDescription,
            ExportedAt = DateTime.UtcNow,
            Automatons = [.. automatons.Select(ConvertToExportItem)]
        };
    }

    private AutomatonExportItemDto ConvertToExportItem(SavedAutomaton automaton)
    {
        var content = DeserializeAutomatonContent(automaton);
        var execState = ProcessExecutionState(automaton);
        var hasExecutionState = automaton.SaveMode == AutomatonSaveMode.WithState;

        return new AutomatonExportItemDto
        {
            Name = automaton.Name,
            Description = automaton.Description,
            HasExecutionState = hasExecutionState,
            Content = content ?? new AutomatonPayloadDto(),
            ExecutionState = execState
        };
    }

    private AutomatonPayloadDto? DeserializeAutomatonContent(SavedAutomaton automaton)
    {
        try
        {
            return JsonSerializer.Deserialize<AutomatonPayloadDto>(automaton.ContentJson);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deserialize content for automaton {Id}", automaton.Id);
            return new AutomatonPayloadDto();
        }
    }

    private SavedExecutionStateDto? ProcessExecutionState(SavedAutomaton automaton)
    {
        if (automaton.SaveMode < AutomatonSaveMode.WithInput || string.IsNullOrEmpty(automaton.ExecutionStateJson))
            return null;

        try
        {
            var fullExecState = JsonSerializer.Deserialize<SavedExecutionStateDto>(automaton.ExecutionStateJson);
            if (fullExecState == null)
                return null;

            return automaton.SaveMode == AutomatonSaveMode.WithInput
                ? CreateInputOnlyExecutionState(fullExecState)
                : fullExecState;
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Failed to deserialize execution state for automaton {Id}", automaton.Id);
            return null;
        }
    }

    private static SavedExecutionStateDto CreateInputOnlyExecutionState(SavedExecutionStateDto fullState)
    {
        return new SavedExecutionStateDto
        {
            Input = fullState.Input,
            Position = 0,
            CurrentStateId = null,
            CurrentStates = null,
            IsAccepted = null,
            StateHistorySerialized = string.Empty,
            StackSerialized = null
        };
    }

    private static string GenerateExportFileName(string groupName)
    {
        return $"{SanitizeFileName(groupName)}_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";
    }

    private void LogExportStart(string groupName, int count)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Exporting group '{GroupName}' with {Count} automaton(s)", groupName, count);
        }
    }

    private void LogExportSuccess(string groupName, string fileName)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Successfully exported group '{GroupName}' to {FileName}", groupName, fileName);
        }
    }

    public async Task<(bool Ok, GroupExportDto? Data, string? Error)> ImportGroupAsync(IFormFile file)
    {
        var validation = ValidateImportFile(file);
        if (!validation.IsValid)
            return (false, null, validation.ErrorMessage);

        LogImportStart(file.FileName);

        var content = await ReadFileContentAsync(file);
        var deserializationResult = DeserializeGroupImport(content, file.FileName);

        if (!deserializationResult.IsSuccess)
            return (false, null, deserializationResult.ErrorMessage);

        var dataValidation = ValidateImportData(deserializationResult.Data!, file.FileName);
        if (!dataValidation.IsValid)
            return (false, null, dataValidation.ErrorMessage);

        LogImportSuccess(deserializationResult.Data!);
        return (true, deserializationResult.Data, null);
    }

    private ImportValidationResult ValidateImportFile(IFormFile? file)
    {
        if (file == null || file.Length == 0)
        {
            logger.LogWarning("ImportGroupAsync called with empty file");
            return ImportValidationResult.Invalid("No file uploaded.");
        }

        return ImportValidationResult.Valid();
    }

    private GroupDeserializationResult DeserializeGroupImport(string content, string fileName)
    {
        try
        {
            var importData = JsonSerializer.Deserialize<GroupExportDto>(content, s_caseInsensitiveDeserializerOptions);

            if (importData == null)
            {
                logger.LogWarning("Failed to deserialize group import file {FileName}", fileName);
                return GroupDeserializationResult.Failure("Invalid group export file format.");
            }

            return GroupDeserializationResult.Success(importData);
        }
        catch (JsonException ex)
        {
            logger.LogError(ex, "JSON parsing error while importing group from {FileName}", fileName);
            return GroupDeserializationResult.Failure("Invalid JSON format in group export file.");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Unexpected error while importing group from {FileName}", fileName);
            return GroupDeserializationResult.Failure("Failed to import group: " + ex.Message);
        }
    }

    private ImportValidationResult ValidateImportData(GroupExportDto importData, string fileName)
    {
        if (importData.Automatons == null || importData.Automatons.Count == 0)
        {
            logger.LogWarning("Group import file {FileName} contains no automatons", fileName);
            return ImportValidationResult.Invalid("Group export file contains no automatons.");
        }

        return ImportValidationResult.Valid();
    }

    private void LogImportStart(string fileName)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Importing group from file {FileName}", fileName);
        }
    }

    private void LogImportSuccess(GroupExportDto importData)
    {
        if (logger.IsEnabled(LogLevel.Information))
        {
            logger.LogInformation("Successfully imported group '{GroupName}' with {Count} automaton(s)",
                importData.GroupName, importData.Automatons.Count);
        }
    }

    private class ImportValidationResult
    {
        public bool IsValid { get; init; }
        public string? ErrorMessage { get; init; }

        public static ImportValidationResult Valid() => new() { IsValid = true };
        public static ImportValidationResult Invalid(string errorMessage) => new() { IsValid = false, ErrorMessage = errorMessage };
    }

    private class GroupDeserializationResult
    {
        public bool IsSuccess { get; init; }
        public GroupExportDto? Data { get; init; }
        public string? ErrorMessage { get; init; }

        public static GroupDeserializationResult Success(GroupExportDto data) => new() { IsSuccess = true, Data = data };
        public static GroupDeserializationResult Failure(string errorMessage) => new() { IsSuccess = false, ErrorMessage = errorMessage };
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
            AutomatonType.DPDA => new DPDA(),
            AutomatonType.NPDA => new NPDA(),
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
                ApplyInputFromJson(model, executionStateJson);
                ResetModelToInputOnly(model);
                break;

            case "state":
                ApplyStateFromJson(model, executionStateJson);
                break;

            default:
                ResetModelToDefaultState(model);
                break;
        }
    }

    public void RestoreExecutionStateFromDto(AutomatonViewModel model, SavedExecutionStateDto? executionState, string mode, AutomatonSaveMode saveMode)
    {
        ArgumentNullException.ThrowIfNull(model);

        switch (mode.ToLowerInvariant())
        {
            case "input":
                ApplyInputFromDto(model, executionState);
                ResetModelToInputOnly(model, setHasExecuted: true);
                break;

            case "state":
                ApplyStateFromDto(model, executionState, saveMode);
                break;

            default:
                ResetModelToDefaultState(model, setHasExecuted: true);
                break;
        }
    }

    private static void ApplyInputFromJson(AutomatonViewModel model, string? executionStateJson)
    {
        if (string.IsNullOrEmpty(executionStateJson))
            return;

        var execState = JsonSerializer.Deserialize<JsonElement>(executionStateJson);
        if (execState.ValueKind != JsonValueKind.Undefined && execState.TryGetProperty("Input", out var input))
        {
            model.Input = input.GetString() ?? string.Empty;
        }
    }

    private static void ApplyStateFromJson(AutomatonViewModel model, string? executionStateJson)
    {
        if (string.IsNullOrEmpty(executionStateJson))
            return;

        var execState = JsonSerializer.Deserialize<JsonElement>(executionStateJson);
        if (execState.ValueKind == JsonValueKind.Undefined)
            return;

        if (execState.TryGetProperty("Input", out var input))
            model.Input = input.GetString() ?? string.Empty;

        if (execState.TryGetProperty("Position", out var pos))
            model.Position = pos.GetInt32();

        if (execState.TryGetProperty("CurrentStateId", out var csid) && csid.ValueKind != JsonValueKind.Null)
            model.CurrentStateId = csid.GetInt32();

        if (execState.TryGetProperty("IsAccepted", out var acc) && acc.ValueKind != JsonValueKind.Null)
            model.IsAccepted = acc.GetBoolean();

        if (execState.TryGetProperty("StateHistorySerialized", out var hist))
            model.StateHistorySerialized = hist.GetString() ?? string.Empty;

        if (execState.TryGetProperty("StackSerialized", out var stack) && stack.ValueKind != JsonValueKind.Null)
            model.StackSerialized = stack.GetString();
    }

    private static void ApplyInputFromDto(AutomatonViewModel model, SavedExecutionStateDto? executionState)
    {
        if (executionState != null)
        {
            model.Input = executionState.Input ?? string.Empty;
        }
    }

    private static void ApplyStateFromDto(AutomatonViewModel model, SavedExecutionStateDto? executionState, AutomatonSaveMode saveMode)
    {
        if (executionState == null)
            return;

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

    private static void ResetModelToInputOnly(AutomatonViewModel model, bool setHasExecuted = false)
    {
        model.Position = 0;
        model.CurrentStateId = null;
        model.CurrentStates = null;
        model.IsAccepted = null;
        model.StateHistorySerialized = string.Empty;
        model.StackSerialized = null;

        if (setHasExecuted)
            model.HasExecuted = false;
    }

    private static void ResetModelToDefaultState(AutomatonViewModel model, bool setHasExecuted = false)
    {
        model.Input = string.Empty;
        model.Position = 0;
        model.CurrentStateId = null;
        model.CurrentStates = null;
        model.IsAccepted = null;
        model.StateHistorySerialized = string.Empty;
        model.StackSerialized = null;

        if (setHasExecuted)
            model.HasExecuted = false;
    }
}
