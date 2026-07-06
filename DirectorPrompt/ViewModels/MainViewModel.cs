using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using DirectorPrompt.Agents;
using DirectorPrompt.Domain.Enums;
using DirectorPrompt.Domain.Models;
using DirectorPrompt.Domain.Repositories;
using DirectorPrompt.Views;
using Microsoft.Extensions.DependencyInjection;
using Serilog;

namespace DirectorPrompt.ViewModels;

public sealed partial class MainViewModel : ObservableObject
{
    private readonly Orchestrator         orchestrator;
    private readonly IProjectRepository   projectRepository;
    private readonly ISessionRepository   sessionRepository;
    private readonly IEventRepository     eventRepository;
    private readonly ISceneRepository     sceneRepository;
    private readonly IStateRepository     stateRepository;
    private readonly ICharacterRepository characterRepository;
    private readonly IDirectiveRepository directiveRepository;
    private readonly IServiceProvider     serviceProvider;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsProjectSelected))]
    private Project? currentProject;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsSessionSelected))]
    private Session? currentSession;

    [ObservableProperty]
    private string statusMessage = "就绪";

    [ObservableProperty]
    private bool isProcessing;

    public bool IsProjectSelected => CurrentProject is not null;

    public bool IsSessionSelected => CurrentSession is not null;

    public DialogViewModel Dialog { get; } = new();

    public DirectiveInputViewModel DirectiveInput { get; } = new();

    public StatePanelViewModel StatePanel { get; } = new();

    public DirectivesPanelViewModel DirectivesPanel { get; } = new();

    public CharacterPanelViewModel CharacterPanel { get; } = new();

    public ObservableCollection<Project> Projects { get; } = [];

    public ObservableCollection<Session> Sessions { get; } = [];

    public ObservableCollection<PipelineStageViewModel> PipelineStages { get; } = [];

    public MainViewModel
    (
        Orchestrator         orchestrator,
        IProjectRepository   projectRepository,
        ISessionRepository   sessionRepository,
        IEventRepository     eventRepository,
        ISceneRepository     sceneRepository,
        IStateRepository     stateRepository,
        ICharacterRepository characterRepository,
        IDirectiveRepository directiveRepository,
        IServiceProvider     serviceProvider
    )
    {
        this.orchestrator        = orchestrator;
        this.projectRepository   = projectRepository;
        this.sessionRepository   = sessionRepository;
        this.eventRepository     = eventRepository;
        this.sceneRepository     = sceneRepository;
        this.stateRepository     = stateRepository;
        this.characterRepository = characterRepository;
        this.directiveRepository = directiveRepository;
        this.serviceProvider     = serviceProvider;
    }

    [RelayCommand]
    private async Task LoadProjectsAsync()
    {
        try
        {
            Log.Information("加载项目列表");

            var projects = await projectRepository.GetAllAsync();

            var previousID = CurrentProject?.ID;

            Projects.Clear();

            foreach (var project in projects)
                Projects.Add(project);

            if (previousID.HasValue)
                CurrentProject = Projects.FirstOrDefault(p => p.ID == previousID.Value);

            Log.Information("项目列表加载完成: 数量={Count}", Projects.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载项目列表失败");
            StatusMessage = $"加载失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task LoadSessionsAsync()
    {
        if (CurrentProject is null)
            return;

        try
        {
            Log.Information("加载对话列表: 项目={ProjectID}", CurrentProject.ID);

            var sessions = await sessionRepository.GetByProjectAsync(CurrentProject.ID);

            var previousID = CurrentSession?.ID;

            Sessions.Clear();

            foreach (var session in sessions)
                Sessions.Add(session);

            if (previousID.HasValue)
                CurrentSession = Sessions.FirstOrDefault(s => s.ID == previousID.Value);

            Log.Information("对话列表加载完成: 数量={Count}", Sessions.Count);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "加载对话列表失败");
            StatusMessage = $"加载失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private async Task NewSessionAsync()
    {
        if (CurrentProject is null)
            return;

        try
        {
            var now = DateTime.UtcNow;

            var session = await sessionRepository.CreateAsync
                          (
                              new Session
                              {
                                  ProjectID = CurrentProject.ID,
                                  Title     = $"对话 {DateTime.Now:MM-dd HH:mm}",
                                  CreatedAt = now,
                                  UpdatedAt = now
                              }
                          );

            await characterRepository.CloneProjectCharactersToSessionAsync(CurrentProject.ID, session.ID);

            Log.Information("创建对话: ID={SessionID}, 项目={ProjectID}", session.ID, CurrentProject.ID);

            await LoadSessionsAsync();
            CurrentSession = Sessions.FirstOrDefault(s => s.ID == session.ID);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "创建对话失败");
            StatusMessage = $"创建对话失败: {ex.Message}";
        }
    }

    [RelayCommand]
    private void NewProject()
    {
        var window = serviceProvider.GetRequiredService<ProjectEditWindow>();
        window.Owner = GetCurrentWindow();

        if (window.ShowDialog() == true)
            _ = LoadProjectsAsync();
    }

    [RelayCommand]
    private async Task EditProjectAsync()
    {
        if (CurrentProject is null)
            return;

        var window = serviceProvider.GetRequiredService<ProjectEditWindow>();
        await window.ViewModel.LoadFromProjectAsync(CurrentProject);
        window.Owner = GetCurrentWindow();

        if (window.ShowDialog() == true)
            await LoadProjectsAsync();
    }

    [RelayCommand]
    private void OpenSettings()
    {
        var window = serviceProvider.GetRequiredService<SettingsWindow>();
        window.Owner = GetCurrentWindow();
        window.ShowDialog();
    }

    private static Window? GetCurrentWindow() => Application.Current.Windows.Cast<Window>().FirstOrDefault(w => w.IsActive);

    private void ResetPipelineStages() =>
        PipelineStages.Clear();

    private void UpdatePipelineStage(string stage, string status, string? detail = null)
    {
        var existing = PipelineStages.FirstOrDefault(s => s.Stage == stage);

        if (existing is not null)
        {
            existing.Status = status;
            existing.Detail = detail;
        }
        else
            PipelineStages.Add(new PipelineStageViewModel { Stage = stage, Status = status, Detail = detail });
    }

    [RelayCommand]
    private async Task SendDirectivesAsync()
    {
        if (CurrentProject is null)
        {
            StatusMessage = "请先选择或创建项目";
            return;
        }

        if (CurrentSession is null)
        {
            StatusMessage = "请先选择或创建对话";
            return;
        }

        if (DirectiveInput.Directives.Count == 0)
        {
            StatusMessage = "请至少添加一条指令";
            return;
        }

        IsProcessing  = true;
        StatusMessage = "处理中…";
        ResetPipelineStages();

        try
        {
            var items = DirectiveInput.Directives
                                      .Select(d => new DirectiveItem(d.Type, d.Content, d.Order))
                                      .ToList();

            var batch = new DirectiveBatch(CurrentProject.ID, items);

            Log.Information
            (
                "用户发送指令: 项目={ProjectID} ({ProjectName}), 对话={SessionID}, 指令数={Count}",
                CurrentProject.ID,
                CurrentProject.Name,
                CurrentSession.ID,
                items.Count
            );

            foreach (var d in items)
                Log.Information("  指令 #{Order} [{Type}] {Content}", d.Order, d.Type, d.Content);

            var directorContent = string.Join("\n", items.Select(d => $"[{d.Type}] {d.Content}"));
            Dialog.AddDirectorEntry(0, directorContent);

            var streamingEntry = Dialog.BeginStreamingNarrative(0);

            var dispatcher = Application.Current.Dispatcher;

            var result = await orchestrator.ProcessBatchAsync
                         (
                             batch,
                             CurrentSession.ID,
                             (narrative, thinking) => { dispatcher.BeginInvoke(new Action(() => { streamingEntry.UpdateStreamingContent(narrative, thinking); })); },
                             update => { dispatcher.BeginInvoke(new Action(() => { UpdatePipelineStage(update.Stage, update.Status, update.Detail); })); }
                         );

            streamingEntry.RoundID  = result.RoundID;
            streamingEntry.Content  = result.Narrative;
            streamingEntry.Thinking = result.Thinking;
            streamingEntry.RenderMarkdown();

            Log.Information
            (
                "指令处理完成: 轮次={RoundID}, 审计通过={Passed}, 叙事长度={NarrativeLen}",
                result.RoundID,
                result.AuditPassed,
                result.Narrative.Length
            );

            if (result.Violations.Count > 0)
            {
                foreach (var v in result.Violations)
                    Log.Warning("  违规: [{Severity}] {Description}", v.Severity, v.Description);
            }

            await RefreshSidebarAsync();

            DirectiveInput.Clear();
            StatusMessage = result.AuditPassed ?
                                "完成" :
                                $"完成 (审计警告: {result.Violations.Count} 条)";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "处理指令失败");
            StatusMessage = $"处理失败: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task DeleteLastRoundAsync()
    {
        if (CurrentSession is null)
            return;

        IsProcessing  = true;
        StatusMessage = "回滚中…";

        try
        {
            var latestRound = await eventRepository.GetLatestRoundIDAsync(CurrentSession.ID);

            if (latestRound <= 0)
            {
                StatusMessage = "没有可回滚的轮次";
                return;
            }

            Log.Information("用户回滚轮次: 对话={SessionID}, 轮次={RoundID}", CurrentSession.ID, latestRound);

            await orchestrator.DeleteRoundAsync(CurrentSession.ID, latestRound);

            Dialog.RemoveEntriesByRound(latestRound);

            await RefreshSidebarAsync();
            StatusMessage = "已回滚";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "回滚失败");
            StatusMessage = $"回滚失败: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    [RelayCommand]
    private async Task RewriteLastRoundAsync()
    {
        if (CurrentSession is null)
            return;

        IsProcessing  = true;
        StatusMessage = "重写中…";

        try
        {
            var latestRound = await eventRepository.GetLatestRoundIDAsync(CurrentSession.ID);

            if (latestRound <= 0)
            {
                StatusMessage = "没有可重写的轮次";
                return;
            }

            var events        = await eventRepository.GetByRoundAsync(latestRound);
            var directorEvent = events.FirstOrDefault(e => e.Type == EventType.DirectorInput);

            if (directorEvent is null)
            {
                StatusMessage = "找不到原始指令";
                return;
            }

            StatusMessage = "重写功能需要原始指令批次, 请重新输入指令后发送";
        }
        catch (Exception ex)
        {
            Log.Error(ex, "重写失败");
            StatusMessage = $"重写失败: {ex.Message}";
        }
        finally
        {
            IsProcessing = false;
        }
    }

    partial void OnCurrentProjectChanged(Project? value)
    {
        CurrentSession = null;
        Sessions.Clear();
        Dialog.Clear();

        if (value is not null)
        {
            Log.Information("切换项目: ID={ProjectID}, 名称={Name}", value.ID, value.Name);
            _ = LoadSessionsAsync();
        }
    }

    partial void OnCurrentSessionChanged(Session? value)
    {
        Dialog.Clear();

        if (value is not null)
        {
            Log.Information("切换对话: ID={SessionID}", value.ID);

            if (CurrentProject is not null && !string.IsNullOrWhiteSpace(CurrentProject.OpeningMessage))
                Dialog.AddOpeningMessage(CurrentProject.OpeningMessage);

            _ = RefreshSidebarAsync();
        }
    }

    private async Task RefreshSidebarAsync()
    {
        if (CurrentSession is null || CurrentProject is null)
            return;

        await RefreshStatePanelAsync();
        await RefreshDirectivesPanelAsync();
        await RefreshCharacterPanelAsync();
    }

    private async Task RefreshStatePanelAsync()
    {
        if (CurrentSession is null || CurrentProject is null)
            return;

        StatePanel.Clear();

        var scene = await sceneRepository.GetActiveSceneAsync(CurrentSession.ID);

        if (scene is not null)
        {
            StatePanel.CurrentSceneLabel = scene.TimeLabel;
            StatePanel.TimelineLabel     = scene.TimelinePosition.ToString();
        }

        var values     = await stateRepository.GetAllStateValuesAsync(CurrentProject.ID, CurrentSession.ID);
        var attributes = await stateRepository.GetAttributesAsync(CurrentProject.ID);

        foreach (var attr in attributes)
        {
            var value = values.FirstOrDefault(v => v.AttributeID == attr.ID);

            StatePanel.StateItems.Add
            (
                new StateItemViewModel
                {
                    Name  = attr.DisplayName,
                    Value = value?.Value ?? "—",
                    Scope = attr.Scope.ToString()
                }
            );
        }

        var flags = await stateRepository.GetFlagsAsync(CurrentSession.ID);

        foreach (var flag in flags)
        {
            StatePanel.StateItems.Add
            (
                new StateItemViewModel
                {
                    Name = flag.DisplayName,
                    Value = flag.Value ?
                                "✓" :
                                "✗",
                    Scope = "Flag"
                }
            );
        }
    }

    private async Task RefreshDirectivesPanelAsync()
    {
        if (CurrentSession is null)
            return;

        DirectivesPanel.Clear();

        var directives = await directiveRepository.GetActiveAsync(CurrentSession.ID);

        foreach (var d in directives)
        {
            DirectivesPanel.Directives.Add
            (
                new DirectivePanelItemViewModel
                {
                    Type = d.Type switch
                    {
                        DirectiveType.Tone                => "🎭",
                        DirectiveType.TemporaryConstraint => "🚫",
                        DirectiveType.SceneChange         => "🎬",
                        _                                 => "📝"
                    },
                    Content = d.Content,
                    HasTTL  = d.TTL.HasValue,
                    TtlLabel = d.TTL.HasValue ?
                                   $"剩余 {d.TTL} 轮" :
                                   "永久"
                }
            );
        }
    }

    private async Task RefreshCharacterPanelAsync()
    {
        if (CurrentSession is null)
            return;

        CharacterPanel.Clear();

        var characters = await characterRepository.GetBySessionAsync(CurrentSession.ID);

        foreach (var c in characters)
        {
            CharacterPanel.Characters.Add
            (
                new CharacterPanelItemViewModel
                {
                    Name        = c.Name,
                    Status      = c.Status.ToString(),
                    Description = c.Description
                }
            );
        }
    }
}

public sealed class PipelineStageViewModel : INotifyPropertyChanged
{
    private string  status = string.Empty;
    private string? detail;

    public string Stage { get; init; } = string.Empty;

    public string Status
    {
        get => status;
        set
        {
            if (status != value)
            {
                status = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Status)));
            }
        }
    }

    public string? Detail
    {
        get => detail;
        set
        {
            if (detail != value)
            {
                detail = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(Detail)));
            }
        }
    }

    public event PropertyChangedEventHandler? PropertyChanged;
}
