namespace DirectorPrompt.Infrastructure.Localization;

public interface ILocalizationSource : IDisposable
{
    bool SupportsChangeNotifications { get; }

    event EventHandler<LocalizationSourceChangedEventArgs>? ResourceChanged;

    IReadOnlyList<string> DiscoverLanguages();

    bool Exists(string language, string resourceName);

    Stream? OpenRead(string language, string resourceName);
}
