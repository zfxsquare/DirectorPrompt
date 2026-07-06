namespace DirectorPrompt.Domain.Configurations;

public record LocalizationConfig
{
    public string Language { get; init; } = "zh-CN";
}
