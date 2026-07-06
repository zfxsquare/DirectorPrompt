using System.ComponentModel;
using DirectorPrompt.Domain.Services;

namespace DirectorPrompt.Localization;

public sealed class Loc : INotifyPropertyChanged
{
    public static Loc Instance { get; } = new();

    private ILocalizationService? service;

    private Loc() { }

    public string this[string key] => service?.Get(key) ?? key;

    public static string CurrentLanguage => Instance.service?.CurrentLanguage ?? "en";

    public static IReadOnlyDictionary<string, string> AvailableLanguages =>
        Instance.service?.AvailableLanguages ?? new Dictionary<string, string>();

    public static string Get(string key) =>
        Instance.service?.Get(key) ?? key;

    public static string Get(string key, params object[] args) =>
        Instance.service?.Get(key, args) ?? key;

    public static void LoadLanguage(string language) =>
        Instance.service?.LoadLanguage(language);

    public void SetService(ILocalizationService localizationService)
    {
        if (service != null)
            service.LanguageChanged -= OnLanguageChanged;

        service                 =  localizationService;
        service.LanguageChanged += OnLanguageChanged;

        OnLanguageChanged();
    }

    private void OnLanguageChanged() =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));

    public event PropertyChangedEventHandler? PropertyChanged;
}
