using DirectorPrompt.Domain.Enums;

namespace DirectorPrompt.Domain.Configurations;

public record AgentDefinition
{
    public AgentRole Role { get; init; }

    public ModelConfig ModelConfig { get; init; } = new();

    public string SystemPrompt { get; init; } = string.Empty;

    public float Temperature { get; init; }

    public string[] Tools { get; init; } = [];
}
