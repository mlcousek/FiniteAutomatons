using Microsoft.AspNetCore.Http;
using FiniteAutomatons.Core.Models.ViewModel;
using FiniteAutomatons.Core.Models.DTOs;
using FiniteAutomatons.Core.Models.Database;

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
}
