using System.Collections.ObjectModel;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DirectorPrompt.Domain.Configurations;
using DirectorPrompt.Domain.Services;
using DirectorPrompt.Infrastructure;
using DirectorPrompt.Localization;
using Serilog;

namespace DirectorPrompt.ViewModels;

public sealed partial class SettingsViewModel : ObservableObject
{
    private static readonly JsonSerializerOptions JSONOptions = new()
    {
        WriteIndented          = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
        {
            new JsonStringEnumConverter()
        }
    };

    private readonly IModelConnectionTester connectionTester;
    private readonly ILocalizationService   localizationService;

    [ObservableProperty]
    public partial bool IsSaving { get; set; }

    [ObservableProperty]
    public partial string ValidationMessage { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string SelectedLanguage { get; set; } = string.Empty;

    public ObservableCollection<AgentSettingViewModel> Agents { get; } = [];

    public IReadOnlyDictionary<string, string> AvailableLanguages =>
        localizationService.AvailableLanguages;

    public SettingsViewModel
    (
        UserSettings           userSettings,
        IModelConnectionTester connectionTester,
        ILocalizationService   localizationService
    )
    {
        this.connectionTester    = connectionTester;
        this.localizationService = localizationService;

        LoadSettings(userSettings);
    }

    private void LoadSettings(UserSettings userSettings)
    {
        SelectedLanguage = userSettings.Localization.Language;

        if (string.IsNullOrEmpty(SelectedLanguage))
            SelectedLanguage = localizationService.CurrentLanguage;

        LoadAgents(userSettings.Orchestrator.Agents);
    }

    private void LoadAgents(List<AgentDefinition> agents)
    {
        if (agents is not { Count: > 0 })
            return;

        foreach (var agent in agents)
        {
            Agents.Add
            (
                new()
                {
                    Role         = agent.Role,
                    Provider     = agent.ModelConfig.Provider,
                    Endpoint     = agent.ModelConfig.Endpoint,
                    APIKey       = agent.ModelConfig.APIKey ?? string.Empty,
                    ModelName    = agent.ModelConfig.ModelName,
                    SystemPrompt = agent.SystemPrompt,
                    Temperature  = agent.Temperature,
                    Tools        = agent.Tools,
                }
            );
        }
    }

    partial void OnSelectedLanguageChanged(string value)
    {
        if (string.IsNullOrEmpty(value) || !AvailableLanguages.ContainsKey(value))
            return;

        if (localizationService.CurrentLanguage != value)
            localizationService.LoadLanguage(value);
    }

    [RelayCommand]
    private async Task SaveAsync()
    {
        IsSaving = true;

        try
        {
            var settings = BuildUserSettings();
            var json     = JsonSerializer.Serialize(settings, JSONOptions);

            Directory.CreateDirectory(AppPaths.DataDirectory);
            await File.WriteAllTextAsync(AppPaths.UserSettingsPath, json);

            ValidationMessage = Loc.Get("Settings.Saved");
        }
        catch (Exception ex)
        {
            Log.Error(ex, "保存设置失败");
            ValidationMessage = Loc.Get("Settings.SaveFailed", ex.Message);
        }
        finally
        {
            IsSaving = false;
        }
    }

    private UserSettings BuildUserSettings()
    {
        var agents = Agents.Select
        (a => new AgentDefinition
            {
                Role = a.Role,
                ModelConfig = new ModelConfig
                {
                    Provider  = a.Provider,
                    Endpoint  = a.Endpoint,
                    APIKey    = a.APIKey,
                    ModelName = a.ModelName
                },
                SystemPrompt = a.SystemPrompt,
                Temperature  = a.Temperature,
                Tools        = a.Tools,
            }
        ).ToList();

        return new UserSettings
        {
            Orchestrator = new UserOrchestratorConfig
            {
                Agents           = agents
            },
            Localization = new LocalizationConfig { Language = SelectedLanguage }
        };
    }

    [RelayCommand]
    private async Task TestAgentConnectionAsync(AgentSettingViewModel? agent)
    {
        if (agent is null)
            return;

        agent.IsTestingConnection = true;
        agent.ConnectionSuccess   = null;
        agent.ConnectionMessage   = Loc.Get("Settings.TestingConnection");

        try
        {
            await connectionTester.TestChatAsync(agent.Provider, agent.Endpoint, agent.APIKey, agent.ModelName);

            agent.ConnectionSuccess = true;
            agent.ConnectionMessage = Loc.Get("Settings.ConnectionSuccess", agent.ModelName);
        }
        catch (Exception ex)
        {
            agent.ConnectionSuccess = false;
            agent.ConnectionMessage = Loc.Get("Settings.ConnectionFailed", ex.Message);
        }
        finally
        {
            agent.IsTestingConnection = false;
        }
    }
}
