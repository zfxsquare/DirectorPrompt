using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Globalization;
using DirectorPrompt.Domain.Services;
using Serilog;

namespace DirectorPrompt.Infrastructure.Localization;

public sealed class LocalizationService : ILocalizationService, IDisposable
{
    private static readonly FrozenDictionary<string, string> EmptyResource =
        new Dictionary<string, string>(StringComparer.Ordinal).ToFrozenDictionary(StringComparer.Ordinal);

    private static readonly ConfiguredState EmptyState = new(null, null, false);

    private ConfiguredState          configuredState = EmptyState;
    private LanguageSnapshot         currentSnapshot = LanguageSnapshot.Empty;
    private CancellationTokenSource? reloadCancelSource;

    public string CurrentLanguage =>
        GetSnapshot().Language;

    public IReadOnlyList<string> SupportedLanguages =>
        GetSnapshot().DiscoveredLanguages;

    public IReadOnlyDictionary<string, string> AvailableLanguages =>
        GetSnapshot().AvailableLanguages;

    public event Action? LanguageChanged;

    public LocalizationService
    (
        LocalizationOptions options,
        string              initialLanguage,
        string              preferredLanguage
    )
    {
        ArgumentNullException.ThrowIfNull(options);
        ValidateOptions(options);

        var nextState     = new ConfiguredState(options, null, true);
        var previousState = Interlocked.Exchange(ref configuredState, nextState);

        try
        {
            CancelReload();
            DisposeConfiguredState(previousState);

            if (options.EnableHotReload && options.Source.SupportsChangeNotifications)
            {
                EventHandler<LocalizationSourceChangedEventArgs> handler = OnLocalizationSourceChanged;
                options.Source.ResourceChanged += handler;

                Volatile.Write(ref configuredState, nextState with { ChangeHandler = handler });
            }

            var normalizedLanguage = NormalizeLanguage(initialLanguage, preferredLanguage);
            LoadLanguage(normalizedLanguage);
        }
        catch
        {
            CancelReload();
            DisposeConfiguredState(Volatile.Read(ref configuredState));
            Volatile.Write(ref configuredState, EmptyState);
            Interlocked.Exchange(ref currentSnapshot, LanguageSnapshot.Empty);
            throw;
        }
    }

    public string NormalizeLanguage(string requestedLanguage, string preferredLanguage)
    {
        var snapshot = GetSnapshot();

        if (snapshot.AvailableLanguages.ContainsKey(requestedLanguage))
            return requestedLanguage;

        if (snapshot.AvailableLanguages.ContainsKey(preferredLanguage))
            return preferredLanguage;

        var options = GetConfiguredState().Options!;

        foreach (var fallbackLanguage in EnumerateFallbackLanguages(options, requestedLanguage))
        {
            if (snapshot.AvailableLanguages.ContainsKey(fallbackLanguage))
                return fallbackLanguage;
        }

        foreach (var fallbackLanguage in EnumerateFallbackLanguages(options, preferredLanguage))
        {
            if (snapshot.AvailableLanguages.ContainsKey(fallbackLanguage))
                return fallbackLanguage;
        }

        return options.DefaultLanguage;
    }

    public void LoadLanguage(string language)
    {
        var state    = GetConfiguredState();
        var options  = state.Options!;
        var snapshot = GetSnapshot();

        if (!snapshot.AvailableLanguages.ContainsKey(language))
            throw new ArgumentOutOfRangeException(nameof(language), $"Language {language} not found in available languages");

        var newSnapshot = BuildSnapshot(options, language);
        Interlocked.Exchange(ref currentSnapshot, newSnapshot);
        LanguageChanged?.Invoke();
    }

    public string Get(string key)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);

        var snapshot = GetSnapshot();
        return TryResolveFormat(snapshot, key, out var format) ?
                   format :
                   LogMissingKeyAndReturnKey(snapshot, key);
    }

    public string Get(string key, params object[] args)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(key);
        ArgumentNullException.ThrowIfNull(args);

        if (args.Length == 0)
            return Get(key);

        var snapshot = GetSnapshot();
        if (!TryResolveFormat(snapshot, key, out var format))
            return LogMissingKeyAndReturnKey(snapshot, key);

        return string.Format(CultureInfo.CurrentCulture, format, args);
    }

    public void Dispose()
    {
        CancelReload();

        var state = Interlocked.Exchange(ref configuredState, EmptyState);
        DisposeConfiguredState(state);

        Interlocked.Exchange(ref currentSnapshot, LanguageSnapshot.Empty);
    }

    private ConfiguredState GetConfiguredState()
    {
        var state = Volatile.Read(ref configuredState);
        if (state.IsConfigured)
            return state;

        throw new InvalidOperationException("Localization service is not configured");
    }

    private LanguageSnapshot GetSnapshot()
    {
        _ = GetConfiguredState();
        return Volatile.Read(ref currentSnapshot);
    }

    private static void ValidateOptions(LocalizationOptions options)
    {
        ArgumentNullException.ThrowIfNull(options.FileNameResolver);
        ArgumentNullException.ThrowIfNull(options.Source);
        ArgumentNullException.ThrowIfNull(options.Parser);
        ArgumentNullException.ThrowIfNull(options.FallbackResolver);

        if (options.ReloadDebounce < TimeSpan.Zero)
            throw new ArgumentOutOfRangeException(nameof(options), "Reload debounce cannot be negative");

        ArgumentException.ThrowIfNullOrWhiteSpace(options.LoggerTag);
    }

    private void CancelReload()
    {
        var current = Interlocked.Exchange(ref reloadCancelSource, null);
        current?.Cancel();
        current?.Dispose();
    }

    private static void DisposeConfiguredState(ConfiguredState state)
    {
        if (!state.IsConfigured || state.Options == null)
            return;

        if (state.ChangeHandler != null)
            state.Options.Source.ResourceChanged -= state.ChangeHandler;

        state.Options.Source.Dispose();
    }

    private async void OnLocalizationSourceChanged(object? sender, LocalizationSourceChangedEventArgs e)
    {
        var state = Volatile.Read(ref configuredState);
        if (!state.IsConfigured || state.Options == null)
            return;

        CancelReload();

        var cancelSource = new CancellationTokenSource();
        var previous     = Interlocked.Exchange(ref reloadCancelSource, cancelSource);
        previous?.Cancel();
        previous?.Dispose();

        try
        {
            await Task.Delay(state.Options.ReloadDebounce, cancelSource.Token).ConfigureAwait(false);

            if (cancelSource.IsCancellationRequested)
                return;

            var currentState = Volatile.Read(ref configuredState);
            if (!currentState.IsConfigured || currentState.Options == null)
                return;

            Log.Debug
            (
                "[{Tag}] Localization resource {Resource} ({ChangeType}) changed, reloading current language",
                currentState.Options.LoggerTag,
                e.ResourceName,
                e.ChangeType
            );

            try
            {
                var currentLang = CurrentLanguage;
                var newSnapshot = BuildSnapshot(currentState.Options, currentLang);

                if (!newSnapshot.AvailableLanguages.ContainsKey(currentLang))
                    currentLang = currentState.Options.DefaultLanguage;

                var normalized = NormalizeLanguage(currentLang, currentState.Options.DefaultLanguage);
                LoadLanguage(normalized);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "[{Tag}] Failed to reload current language", currentState.Options.LoggerTag);
            }
        }
        catch (OperationCanceledException)
        {
            // ignored
        }
    }

    private LanguageSnapshot BuildSnapshot(LocalizationOptions options, string language)
    {
        var discoveredLanguages = options.Source.DiscoverLanguages();
        var availableLanguages  = BuildAvailableLanguages(options, discoveredLanguages);

        var resourceLanguages = EnumerateResourceLanguages(options, language, discoveredLanguages, availableLanguages);
        var resources         = new List<FrozenDictionary<string, string>>(resourceLanguages.Count);

        foreach (var resourceLanguage in resourceLanguages)
        {
            var resource = LoadLanguageResource(options, resourceLanguage);
            if (resource.Count == 0)
                continue;

            resources.Add(resource);
        }

        return new
        (
            language,
            discoveredLanguages,
            availableLanguages,
            [.. resources],
            options.LoggerTag
        );
    }

    private static List<string> EnumerateResourceLanguages
    (
        LocalizationOptions                 options,
        string                              language,
        IReadOnlyList<string>               discoveredLanguages,
        IReadOnlyDictionary<string, string> availableLanguages
    )
    {
        HashSet<string> deduped = new(StringComparer.OrdinalIgnoreCase);
        List<string>    ordered = [];

        AddLanguage(language);

        foreach (var fallbackLanguage in EnumerateFallbackLanguages(options, language))
            AddLanguage(fallbackLanguage);

        AddLanguage(options.DefaultLanguage);

        return ordered;

        void AddLanguage(string value)
        {
            if (!availableLanguages.ContainsKey(value))
                return;

            if (!deduped.Add(value))
                return;

            ordered.Add(value);
        }
    }

    private static IEnumerable<string> EnumerateFallbackLanguages(LocalizationOptions options, string language)
    {
        foreach (var fallbackLanguage in options.FallbackResolver(language))
            yield return fallbackLanguage;
    }

    private static bool TryResolveFormat(LanguageSnapshot snapshot, string key, out string format)
    {
        foreach (var resource in snapshot.Resources)
        {
            if (resource.TryGetValue(key, out format))
                return true;
        }

        format = string.Empty;
        return false;
    }

    private FrozenDictionary<string, string> LoadLanguageResource(LocalizationOptions options, string language)
    {
        try
        {
            var resourceName = options.FileNameResolver(language);
            if (string.IsNullOrWhiteSpace(resourceName))
                throw new InvalidOperationException($"File name for language {language} cannot be empty");

            using var stream = options.Source.OpenRead(language, resourceName);
            if (stream == null)
                return EmptyResource;

            var resource = options.Parser.Parse(stream);
            return resource.Count == 0 ?
                       EmptyResource :
                       resource;
        }
        catch (Exception ex)
        {
            Log.Error(ex, "[{Tag}] Failed to load {Language} language data", options.LoggerTag, language);
            return EmptyResource;
        }
    }

    private static FrozenDictionary<string, string> BuildAvailableLanguages
    (
        LocalizationOptions   options,
        IReadOnlyList<string> discoveredLanguages
    )
    {
        Dictionary<string, string> availableLanguages = new(StringComparer.OrdinalIgnoreCase);

        foreach (var language in discoveredLanguages)
        {
            var resourceName = options.FileNameResolver(language);
            if (string.IsNullOrWhiteSpace(resourceName))
                continue;

            if (!options.Source.Exists(language, resourceName))
                continue;

            availableLanguages[language] = ResolveDisplayName(options, language);
        }

        return availableLanguages.Count == 0 ?
                   FrozenDictionary<string, string>.Empty :
                   availableLanguages.ToFrozenDictionary(StringComparer.OrdinalIgnoreCase);
    }

    private static string ResolveDisplayName(LocalizationOptions options, string language)
    {
        if (options.DisplayNameResolver != null)
            return options.DisplayNameResolver(language);

        try
        {
            return CultureInfo.GetCultureInfo(language).NativeName;
        }
        catch (CultureNotFoundException)
        {
            return language;
        }
    }

    private static string LogMissingKeyAndReturnKey(LanguageSnapshot snapshot, string key)
    {
        if (snapshot.MissingKeys.TryAdd(key, 0))
            Log.Error("[{Tag}] Localization key {Key} not found in current language chain", snapshot.LoggerTag, key);

        return key;
    }

    private sealed record ConfiguredState
    (
        LocalizationOptions?                              Options,
        EventHandler<LocalizationSourceChangedEventArgs>? ChangeHandler,
        bool                                              IsConfigured
    );

    private sealed class LanguageSnapshot
    (
        string                             language,
        IReadOnlyList<string>              discoveredLanguages,
        FrozenDictionary<string, string>   availableLanguages,
        FrozenDictionary<string, string>[] resources,
        string                             loggerTag
    )
    {
        public static LanguageSnapshot Empty { get; } =
            new("en", [], FrozenDictionary<string, string>.Empty, [], nameof(LocalizationService));

        public string Language { get; } = language;

        public IReadOnlyList<string> DiscoveredLanguages { get; } = discoveredLanguages;

        public FrozenDictionary<string, string> AvailableLanguages { get; } = availableLanguages;

        public FrozenDictionary<string, string>[] Resources { get; } = resources;

        public string LoggerTag { get; } = loggerTag;

        public ConcurrentDictionary<string, byte> MissingKeys { get; } = new(StringComparer.Ordinal);
    }
}
