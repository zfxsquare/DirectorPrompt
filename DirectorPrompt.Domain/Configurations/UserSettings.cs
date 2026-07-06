namespace DirectorPrompt.Domain.Configurations;

public record UserSettings
{
    public DatabaseConfig Database { get; init; } = new();

    public UserOrchestratorConfig Orchestrator { get; init; } = new();

    public LocalizationConfig Localization { get; init; } = new();
}
