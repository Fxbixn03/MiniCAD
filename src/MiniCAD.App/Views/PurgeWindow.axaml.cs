using Avalonia.Controls;
using Avalonia.Interactivity;
using MiniCAD.Core.Documents;

namespace MiniCAD.App.Views;

/// <summary>
/// Lets the user choose which categories of unused definition to purge and previews how many of
/// each would be removed before confirming. Closes with a <see cref="PurgeOptions"/> result on
/// "Bereinigen", or <c>null</c> on cancel (#233).
/// </summary>
public partial class PurgeWindow : Window
{
    private readonly CadDocument? _document;

    public PurgeWindow()
    {
        InitializeComponent();
    }

    public PurgeWindow(CadDocument document) : this()
    {
        _document = document;

        foreach (CheckBox check in new[]
                 {
                     LayersCheck, PartialDrawingsCheck, TextStylesCheck,
                     DimStylesCheck, BlocksCheck, PatternsCheck,
                 })
        {
            check.IsCheckedChanged += (_, _) => UpdatePreview();
        }

        UpdatePreview();
    }

    private PurgeOptions BuildOptions() => new()
    {
        Layers = LayersCheck.IsChecked == true,
        EmptyPartialDrawings = PartialDrawingsCheck.IsChecked == true,
        TextStyles = TextStylesCheck.IsChecked == true,
        DimStyles = DimStylesCheck.IsChecked == true,
        BlockDefinitions = BlocksCheck.IsChecked == true,
        Patterns = PatternsCheck.IsChecked == true,
    };

    private void UpdatePreview()
    {
        if (_document is null)
            return;

        PurgeReport report = _document.FindPurgeable(BuildOptions());
        PreviewText.Text = report.IsEmpty
            ? "Nichts zu bereinigen."
            : $"Wird entfernt: {report.Summary()}.";
        PurgeButton.IsEnabled = !report.IsEmpty;
    }

    private void OnPurge(object? sender, RoutedEventArgs e) => Close(BuildOptions());

    private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
}
