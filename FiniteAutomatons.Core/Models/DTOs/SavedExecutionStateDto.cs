namespace FiniteAutomatons.Core.Models.DTOs;

public sealed class SavedExecutionStateDto
{
    public string? Input { get; set; }
    public int Position { get; set; }
    public int? CurrentStateId { get; set; }
    public List<int>? CurrentStates { get; set; }
    public bool? IsAccepted { get; set; }
    public string? StateHistorySerialized { get; set; }
    public string? StackSerialized { get; set; }
}
