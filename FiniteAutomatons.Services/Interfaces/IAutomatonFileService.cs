using FiniteAutomatons.Core.Models.Database;
using FiniteAutomatons.Core.Models.DTOs;
using FiniteAutomatons.Core.Models.ViewModel;
using Microsoft.AspNetCore.Http;

namespace FiniteAutomatons.Services.Interfaces;

public interface IAutomatonFileService
{
    /// <summary>
    /// Loads an automaton from a file (JSON or text format). Only structure is loaded, no execution state.
    /// </summary>
    Task<(bool Ok, AutomatonViewModel? Model, string? Error)> LoadFromFileAsync(IFormFile file);

    /// <summary>
    /// Loads an automaton with full view model including execution state if present.
    /// Falls back to domain parsing if not a full view model JSON.
    /// </summary>
    Task<(bool Ok, AutomatonViewModel? Model, string? Error)> LoadViewModelWithStateAsync(IFormFile file);

    /// <summary>
    /// Exports only the automaton structure (states and transitions) as JSON.
    /// </summary>
    (string FileName, string Content) ExportJson(AutomatonViewModel model);

    /// <summary>
    /// Exports the complete view model including any execution state as JSON.
    /// </summary>
    (string FileName, string Content) ExportJsonWithState(AutomatonViewModel model);

    /// <summary>
    /// Exports only the automaton structure in custom text format.
    /// </summary>
    (string FileName, string Content) ExportText(AutomatonViewModel model);

    /// <summary>
    /// Exports the automaton structure with input string but clears execution state.
    /// Useful for saving an automaton ready to be executed with specific input.
    /// </summary>
    (string FileName, string Content) ExportWithInput(AutomatonViewModel model);

    /// <summary>
    /// Exports the automaton with full execution state (position, current state, history, etc.).
    /// Useful for saving a snapshot of automaton mid-execution.
    /// </summary>
    (string FileName, string Content) ExportWithExecutionState(AutomatonViewModel model);

    /// <summary>
    /// Exports a group of saved automatons with all their data including execution states.
    /// </summary>
    /// <param name="groupName">Name of the group being exported</param>
    /// <param name="groupDescription">Optional description of the group</param>
    /// <param name="automatons">List of saved automatons to export</param>
    /// <returns>Tuple of filename and JSON content</returns>
    (string FileName, string Content) ExportGroup(string groupName, string? groupDescription, List<SavedAutomaton> automatons);

    /// <summary>
    /// Imports a group export file and returns the parsed data.
    /// </summary>
    /// <param name="file">The uploaded group export JSON file</param>
    /// <returns>Tuple indicating success, parsed DTO, and error message if any</returns>
    Task<(bool Ok, GroupExportDto? Data, string? Error)> ImportGroupAsync(IFormFile file);

    /// <summary>
    /// Restores execution state from JSON to the automaton model based on the specified mode.
    /// </summary>
    /// <param name="model">The automaton model to restore state to</param>
    /// <param name="executionStateJson">JSON string containing execution state</param>
    /// <param name="mode">Restoration mode: 'structure' (clear all), 'input' (input only), 'state' (full state)</param>
    void RestoreExecutionState(AutomatonViewModel model, string? executionStateJson, string mode);

    /// <summary>
    /// Restores execution state from DTO to the automaton model based on the specified mode and save mode.
    /// </summary>
    /// <param name="model">The automaton model to restore state to</param>
    /// <param name="executionState">Execution state DTO</param>
    /// <param name="mode">Restoration mode: 'structure' (clear all), 'input' (input only), 'state' (full state)</param>
    /// <param name="saveMode">The save mode that was used when saving</param>
    void RestoreExecutionStateFromDto(AutomatonViewModel model, SavedExecutionStateDto? executionState, string mode, AutomatonSaveMode saveMode);
}
