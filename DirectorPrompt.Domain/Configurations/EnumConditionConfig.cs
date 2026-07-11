namespace DirectorPrompt.Domain.Configurations;

public sealed record EnumConditionConfig
{
    public string When { get; init; } = string.Empty;

    public Dictionary<string, float> Transition { get; init; } = [];
}
