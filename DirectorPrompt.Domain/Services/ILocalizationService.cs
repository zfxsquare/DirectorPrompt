namespace DirectorPrompt.Domain.Services;

public interface ILocalizationService
{
    string CurrentLanguage { get; }

    IReadOnlyList<string> SupportedLanguages { get; }

    IReadOnlyDictionary<string, string> AvailableLanguages { get; }

    event Action? LanguageChanged;

    void LoadLanguage(string language);

    string NormalizeLanguage(string requestedLanguage, string preferredLanguage);

    string Get(string key);

    string Get(string key, params object[] args);
}
