namespace DirectorPrompt.Domain.Models;

public record CharacterRelation
{
    public long ID { get; init; }

    public long ProjectID { get; init; }

    public long SessionID { get; init; }

    public long SourceCharacterID { get; init; }

    public long TargetCharacterID { get; init; }

    public string RelationType { get; init; } = string.Empty;

    public string? Description { get; init; }

    public float? Intensity { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}
