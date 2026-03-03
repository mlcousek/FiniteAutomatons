namespace FiniteAutomatons.Core.Models.Database;

public class SharedAutomaton
{
    public int Id { get; set; }

    public required string CreatedByUserId { get; set; }

    public required string Name { get; set; }

    public string? Description { get; set; }

    public required string ContentJson { get; set; }

    public AutomatonSaveMode SaveMode { get; set; }

    public string? ExecutionStateJson { get; set; }

    public string? LayoutJson { get; set; }

    public string? ThumbnailBase64 { get; set; }

    public string? SourceRegex { get; set; }

    public DateTime CreatedAt { get; set; }

    public DateTime? ModifiedAt { get; set; }

    public string? ModifiedByUserId { get; set; }

    public ICollection<SharedAutomatonGroupAssignment> Assignments { get; set; } = [];

    public bool HasInput() => SaveMode >= AutomatonSaveMode.WithInput;
}
