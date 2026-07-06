namespace DirectorPrompt.Domain.Configurations;

public record UserOrchestratorConfig
{
    public List<AgentDefinition> Agents { get; init; } = [];

    public int SnapshotInterval { get; init; }
}
