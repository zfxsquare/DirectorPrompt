using DirectorPrompt.Domain.Enums;

namespace DirectorPrompt.Domain.Models;

public record Character
{
    public long ID { get; init; }

    public long ProjectID { get; init; }

    public long SessionID { get; init; }

    public string Name { get; init; } = string.Empty;

    public string Description { get; init; } = string.Empty;

    public string[] Aliases { get; init; } = [];

    public long[] CategoryIDs { get; init; } = [];

    public CharacterStatus Status { get; init; } = CharacterStatus.Active;

    public int TouchCount { get; init; }

    public long LastTouchedRound { get; init; }

    public string? ContentHash { get; init; }

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}
