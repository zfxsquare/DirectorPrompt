using System.Windows;
using DirectorPrompt.Localization;
using DirectorPrompt.Markdown;
using Wpf.Ui.Controls;

namespace DirectorPrompt.Views;

public partial class ChangelogWindow : FluentWindow
{
    public ChangelogWindow(string changelog, string version)
    {
        InitializeComponent();

        WindowTitleBar.Title = Loc.Get("Changelog.Title");
        CloseButton.Content  = Loc.Get("Common.Close");

        ChangelogViewer.Document = MarkdownRenderer.Render(changelog);
    }

    private void OnCloseClick(object sender, RoutedEventArgs e) =>
        Close();
}
