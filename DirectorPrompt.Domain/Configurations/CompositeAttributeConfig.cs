namespace DirectorPrompt.Domain.Configurations;

public sealed record CompositeAttributeConfig
{
    public string? GenerationGuide { get; init; }

    public string? RegenerateTrigger { get; init; }

    public string? RegenerateCondition { get; init; }
}
