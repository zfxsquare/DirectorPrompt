namespace DirectorPrompt.Domain.Configurations;

public record DatabaseConfig
{
    public string Path { get; init; } = "data/director.db";
}
