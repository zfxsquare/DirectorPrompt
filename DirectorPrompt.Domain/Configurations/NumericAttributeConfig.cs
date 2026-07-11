namespace DirectorPrompt.Domain.Configurations;

public sealed record NumericAttributeConfig
{
    public float? Min { get; init; }

    public float? Max { get; init; }

    public string? Unit { get; init; }

    public string? ChangeRules { get; init; }
}
