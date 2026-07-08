namespace DirectorPrompt.Domain.Configurations;

public record OrchestratorConfig
{
    public List<AgentDefinition> Agents { get; set; } = [];

    public AuditConfig AuditConfig { get; init; } = new();

    public MemoryConfig MemoryConfig { get; init; } = new();

    public KnowledgeRetrievalConfig KnowledgeConfig { get; init; } = new();
}
