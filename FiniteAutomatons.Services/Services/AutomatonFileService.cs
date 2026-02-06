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
        logger.LogInformation("Loaded automaton from file {Name}: Type={Type} States={States} Transitions={Trans}", file.FileName, vm.Type, vm.States.Count, vm.Transitions.Count);
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

        // Fallback to domain parsing
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
        var content = System.Text.Json.JsonSerializer.Serialize(model, new JsonSerializerOptions { WriteIndented = true });
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

        var content = System.Text.Json.JsonSerializer.Serialize(exportModel, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
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

        var content = System.Text.Json.JsonSerializer.Serialize(model, new System.Text.Json.JsonSerializerOptions { WriteIndented = true });
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

        logger.LogInformation("Exporting group '{GroupName}' with {Count} automaton(s)", groupName, automatons.Count);

        var exportData = new GroupExportDto
        {
            GroupName = groupName,
            GroupDescription = groupDescription,
            ExportedAt = DateTime.UtcNow,
            Automatons = automatons.Select(a =>
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

                // Handle execution state based on SaveMode
                if (a.SaveMode >= AutomatonSaveMode.WithInput && !string.IsNullOrEmpty(a.ExecutionStateJson))
                {
                    try
                    {
                        var fullExecState = JsonSerializer.Deserialize<SavedExecutionStateDto>(a.ExecutionStateJson);
                        if (fullExecState != null)
                        {
                            if (a.SaveMode == AutomatonSaveMode.WithInput)
                            {
                                // Export only input, clear execution state
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
                                // Export full execution state
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
            }).ToList()
        };

        var json = JsonSerializer.Serialize(exportData, new JsonSerializerOptions { WriteIndented = true });
        var fileName = $"{SanitizeFileName(groupName)}_export_{DateTime.UtcNow:yyyyMMdd_HHmmss}.json";

        logger.LogInformation("Successfully exported group '{GroupName}' to {FileName}", groupName, fileName);
        return (fileName, json);
    }

    public async Task<(bool Ok, GroupExportDto? Data, string? Error)> ImportGroupAsync(IFormFile file)
    {
        if (file == null || file.Length == 0)
        {
            logger.LogWarning("ImportGroupAsync called with empty file");
            return (false, null, "No file uploaded.");
        }

        logger.LogInformation("Importing group from file {FileName}", file.FileName);

        try
        {
            string content;
            using (var ms = new MemoryStream())
            {
                await file.CopyToAsync(ms);
                content = Encoding.UTF8.GetString(ms.ToArray());
            }

            var importData = JsonSerializer.Deserialize<GroupExportDto>(content, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            });

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

            logger.LogInformation("Successfully imported group '{GroupName}' with {Count} automaton(s)",
                importData.GroupName, importData.Automatons.Count);

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
        var invalid = Path.GetInvalidFileNameChars();
        var sanitized = string.Join("_", fileName.Split(invalid, StringSplitOptions.RemoveEmptyEntries));
        return string.IsNullOrWhiteSpace(sanitized) ? "group" : sanitized;
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
}
