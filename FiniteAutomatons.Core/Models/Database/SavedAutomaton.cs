namespace FiniteAutomatons.Core.Models.Database;

public class SavedAutomaton
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty; // Identity user id (creator)
    public int? GroupId { get; set; }
    public SavedAutomatonGroup? Group { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ContentJson { get; set; } = string.Empty; // serialized automaton (domain-focused JSON)
    public bool HasExecutionState { get; set; } = false;
    public string? ExecutionStateJson { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<SavedAutomatonGroupAssignment> Assignments { get; set; } = [];

    /// <summary>
    /// Checks if this automaton was saved with input (either just input or with execution state).
    /// </summary>
    public bool HasInput()
    {
        if (string.IsNullOrEmpty(ExecutionStateJson))
            return false;

        try
        {
            using var doc = System.Text.Json.JsonDocument.Parse(ExecutionStateJson);
            if (doc.RootElement.TryGetProperty("Input", out var inputProp))
            {
                var input = inputProp.GetString();
                return !string.IsNullOrEmpty(input);
            }
        }
        catch
        {
            // If parsing fails, assume no input
        }

        return false;
    }
}


