namespace DirectorPrompt.Domain.Configurations;

public sealed record EnumAttributeConfig
{
    public List<string> Options { get; init; } = [];

    public string? Trigger { get; init; }

    public Dictionary<string, Dictionary<string, float>> TransitionRules { get; init; } = [];

    public List<EnumConditionConfig> Conditions { get; init; } = [];
}
