using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;

namespace MiniCAD.Core.Commands;

/// <summary>
/// Records a direct (grip) edit of a single entity as an undoable step. The geometry is
/// captured as opaque mementos before and after the drag; the edit is already applied when
/// the command is created, so it is normally <see cref="UndoRedoManager.Push"/>ed rather than
/// executed.
/// </summary>
public sealed class EditEntityCommand : IUndoableCommand
{
    private readonly ICadDocument _document;
    private readonly IEditableEntity _entity;
    private readonly object _before;
    private readonly object _after;

    public EditEntityCommand(ICadDocument document, IEditableEntity entity, object before, object after)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
        _before = before ?? throw new ArgumentNullException(nameof(before));
        _after = after ?? throw new ArgumentNullException(nameof(after));
    }

    public string Name => "Bearbeiten";

    public void Execute() => Apply(_after);

    public void Undo() => Apply(_before);

    private void Apply(object state)
    {
        _entity.RestoreState(state);
        if (_entity is IEntity entity)
            _document.NotifyEntityModified(entity);
    }
}
