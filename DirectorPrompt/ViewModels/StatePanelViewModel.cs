using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using DirectorPrompt.Localization;

namespace DirectorPrompt.ViewModels;

public sealed partial class StateItemViewModel : ObservableObject
{
    [ObservableProperty]
    private string name = string.Empty;

    [ObservableProperty]
    private string value = string.Empty;

    [ObservableProperty]
    private string scope = string.Empty;
}

public sealed partial class StatePanelViewModel : ObservableObject
{
    [ObservableProperty]
    private string currentSceneLabel = Loc.Get("State.Panel.NotStarted");

    [ObservableProperty]
    private string timelineLabel = "—";

    public ObservableCollection<StateItemViewModel> StateItems { get; } = [];

    public void Clear()
    {
        StateItems.Clear();
        CurrentSceneLabel = Loc.Get("State.Panel.NotStarted");
        TimelineLabel     = "—";
    }
}
