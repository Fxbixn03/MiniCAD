using MiniCAD.Core.Documents;

namespace MiniCAD.Core.Commands;

/// <summary>
/// Removes a fixed set of unused definitions (the <see cref="PurgeReport"/>) from a document and
/// puts them back on undo. The report is captured up front so execute/redo and undo all act on
/// the very same instances, keeping the history reversible (#233).
/// </summary>
public sealed class PurgeCommand : IUndoableCommand
{
    private readonly CadDocument _document;
    private readonly PurgeReport _report;

    public PurgeCommand(CadDocument document, PurgeReport report)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _report = report ?? throw new ArgumentNullException(nameof(report));
    }

    public string Name => "Bereinigen";

    public void Execute() => _document.RemovePurged(_report);

    public void Undo() => _document.InsertPurged(_report);
}
