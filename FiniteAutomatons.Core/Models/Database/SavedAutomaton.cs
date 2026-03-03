namespace FiniteAutomatons.Core.Models.Database;

public class SavedAutomaton
{
    public int Id { get; set; }
    public string UserId { get; set; } = string.Empty;
    public int? GroupId { get; set; }
    public SavedAutomatonGroup? Group { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Description { get; set; }
    public string ContentJson { get; set; } = string.Empty;

    public AutomatonSaveMode SaveMode { get; set; } = AutomatonSaveMode.Structure;

    public string? ExecutionStateJson { get; set; }

    public string? LayoutJson { get; set; }

    public string? ThumbnailBase64 { get; set; }

    public string? SourceRegex { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public List<SavedAutomatonGroupAssignment> Assignments { get; set; } = [];

    public bool HasInput() => SaveMode >= AutomatonSaveMode.WithInput;
}
