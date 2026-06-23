using Avalonia.Controls;
using Avalonia.Interactivity;
using MiniCAD.App.ViewModels;

namespace MiniCAD.App.Views;

/// <summary>Find &amp; replace across the document's text objects, applied as one undoable step (#238).</summary>
public partial class FindReplaceWindow : Window
{
    private readonly MainWindowViewModel? _viewModel;

    public FindReplaceWindow()
    {
        InitializeComponent();
    }

    public FindReplaceWindow(MainWindowViewModel viewModel) : this()
    {
        _viewModel = viewModel;
    }

    private bool MatchCase => MatchCaseCheck.IsChecked == true;

    private bool WholeWord => WholeWordCheck.IsChecked == true;

    private void OnCount(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        string query = FindBox.Text ?? string.Empty;
        if (query.Length == 0)
        {
            ResultText.Text = "Suchbegriff eingeben.";
            return;
        }

        int count = _viewModel.CountTextMatches(query, MatchCase, WholeWord);
        ResultText.Text = $"{count} Textobjekt(e) enthalten „{query}“.";
    }

    private void OnReplaceAll(object? sender, RoutedEventArgs e)
    {
        if (_viewModel is null)
            return;

        string query = FindBox.Text ?? string.Empty;
        if (query.Length == 0)
        {
            ResultText.Text = "Suchbegriff eingeben.";
            return;
        }

        int replaced = _viewModel.ReplaceAllText(query, ReplaceBox.Text ?? string.Empty, MatchCase, WholeWord);
        ResultText.Text = replaced == 0
            ? "Keine Treffer ersetzt."
            : $"{replaced} Textobjekt(e) ersetzt.";
    }

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
