namespace DirectorPrompt.Domain.Models;

public record Flag
{
    public long ID { get; init; }

    public long ProjectID { get; init; }

    public long SessionID { get; init; }

    public string Name { get; init; } = string.Empty;

    public string DisplayName { get; init; } = string.Empty;

    public bool Value { get; init; }

    public long? SetAtSceneID { get; init; }
}
