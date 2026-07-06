namespace DirectorPrompt.Domain.Models;

public record Session
{
    public long ID { get; init; }

    public long ProjectID { get; init; }

    public string Title { get; init; } = string.Empty;

    public DateTime CreatedAt { get; init; }

    public DateTime UpdatedAt { get; init; }
}
