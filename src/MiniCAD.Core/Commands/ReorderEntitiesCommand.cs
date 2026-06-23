using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;

namespace MiniCAD.Core.Commands;

/// <summary>
/// Changes the paint order of a document by swapping its entity ordering between two snapshots
/// (#197). Capturing both orders makes any rearrangement — bring to front/back, one step
/// forward/backward, single or multi-selection — reversible in one step.
/// </summary>
public sealed class ReorderEntitiesCommand : IUndoableCommand
{
    private readonly CadDocument _document;
    private readonly IReadOnlyList<IEntity> _before;
    private readonly IReadOnlyList<IEntity> _after;

    public ReorderEntitiesCommand(CadDocument document, IReadOnlyList<IEntity> before, IReadOnlyList<IEntity> after)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _before = before ?? throw new ArgumentNullException(nameof(before));
        _after = after ?? throw new ArgumentNullException(nameof(after));
    }

    public string Name => "Zeichenreihenfolge ändern";

    public void Execute() => _document.SetEntityOrder(_after);

    public void Undo() => _document.SetEntityOrder(_before);
}
