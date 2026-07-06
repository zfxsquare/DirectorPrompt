namespace DirectorPrompt.Infrastructure.Localization;

public sealed class LocalizationOptions
{
    public required string DefaultLanguage { get; init; }

    public required Func<string, string> FileNameResolver { get; init; }

    public required ILocalizationSource Source { get; init; }

    public required ILocalizationParser Parser { get; init; }

    public required Func<string, IEnumerable<string>> FallbackResolver { get; init; }

    public Func<string, string>? DisplayNameResolver { get; init; }

    public bool EnableHotReload { get; init; } = true;

    public TimeSpan ReloadDebounce { get; init; } = TimeSpan.FromSeconds(3);

    public string LoggerTag { get; init; } = nameof(LocalizationService);
}
