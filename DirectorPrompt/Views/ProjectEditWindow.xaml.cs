using System.Windows;
using System.Windows.Controls;
using DirectorPrompt.ViewModels;
using Wpf.Ui.Controls;
using ListViewItem = System.Windows.Controls.ListViewItem;

namespace DirectorPrompt.Views;

public partial class ProjectEditWindow : FluentWindow
{
    public ProjectEditViewModel ViewModel { get; }

    public ProjectEditWindow(ProjectEditViewModel viewModel)
    {
        ViewModel   = viewModel;
        DataContext = viewModel;
        InitializeComponent();
    }

    private void OnNavSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (BasicPanel is null)
            return;

        if (NavList.SelectedItem is not ListViewItem item)
            return;

        var tag = item.Tag as string;

        BasicPanel.Visibility = tag == "basic" ?
                                    Visibility.Visible :
                                    Visibility.Collapsed;
        EmbeddingPanel.Visibility = tag == "embedding" ?
                                        Visibility.Visible :
                                        Visibility.Collapsed;
        KnowledgePanel.Visibility = tag == "knowledge" ?
                                        Visibility.Visible :
                                        Visibility.Collapsed;
        StatePanel.Visibility = tag == "state" ?
                                    Visibility.Visible :
                                    Visibility.Collapsed;
        AuditPanel.Visibility = tag == "audit" ?
                                    Visibility.Visible :
                                    Visibility.Collapsed;
        MemoryPanel.Visibility = tag == "memory" ?
                                     Visibility.Visible :
                                     Visibility.Collapsed;
        RetrievalPanel.Visibility = tag == "retrieval" ?
                                        Visibility.Visible :
                                        Visibility.Collapsed;
    }

    private void OnEditKnowledgeEntry(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: KnowledgeEntryEditViewModel entry })
            entry.IsEditing = !entry.IsEditing;
    }

    private void OnDeleteKnowledgeEntry(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: KnowledgeEntryEditViewModel entry })
            ViewModel.DeleteKnowledgeEntryCommand.Execute(entry);
    }

    private void OnEditStateAttribute(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StateAttributeEditViewModel attr })
            attr.IsEditing = !attr.IsEditing;
    }

    private void OnDeleteStateAttribute(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement { Tag: StateAttributeEditViewModel attr })
            ViewModel.DeleteStateAttributeCommand.Execute(attr);
    }

    private async void OnSaveClick(object sender, RoutedEventArgs e)
    {
        await ViewModel.SaveCommand.ExecuteAsync(null);

        if (ViewModel.SaveSuccess)
        {
            DialogResult = true;
            Close();
        }
    }

    private void OnCancelClick(object sender, RoutedEventArgs e)
    {
        DialogResult = false;
        Close();
    }
}
