using Avalonia.Controls;
using Avalonia.Interactivity;
using MiniCAD.Core.Documents;

namespace MiniCAD.App.Views;

/// <summary>
/// Runs the document audit and lists its findings, offering a check-only pass and a
/// "check &amp; repair" pass that heals orphaned references and drops corrupt objects (#234).
/// </summary>
public partial class DocumentAuditWindow : Window
{
    private readonly CadDocument? _document;

    public DocumentAuditWindow()
    {
        InitializeComponent();
    }

    public DocumentAuditWindow(CadDocument document) : this()
    {
        _document = document;
        RunAudit(repair: false);
    }

    private void RunAudit(bool repair)
    {
        if (_document is null)
            return;

        DocumentAudit audit = DocumentAudit.Run(_document, repair);
        FindingsHost.ItemsSource = audit.Findings;

        if (!audit.HasIssues)
        {
            SummaryText.Text = "Keine Probleme gefunden – das Dokument ist konsistent.";
        }
        else if (repair)
        {
            SummaryText.Text = $"{audit.Findings.Count} Befund(e), {audit.RepairedCount} repariert.";
        }
        else
        {
            SummaryText.Text = $"{audit.Findings.Count} Befund(e) gefunden. „Prüfen & reparieren“ behebt sie.";
        }
    }

    private void OnRecheck(object? sender, RoutedEventArgs e) => RunAudit(repair: false);

    private void OnRepair(object? sender, RoutedEventArgs e) => RunAudit(repair: true);

    private void OnClose(object? sender, RoutedEventArgs e) => Close();
}
