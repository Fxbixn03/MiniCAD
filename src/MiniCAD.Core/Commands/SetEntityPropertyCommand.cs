using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;

namespace MiniCAD.Core.Commands;

/// <summary>
/// A small, reusable command for changing a single non-geometric property of an entity
/// (its layer, Teilbild, stroke override or fill pattern). The caller supplies closures that
/// apply and revert the change; the command takes care of the undo bookkeeping and the
/// change notification, keeping the App free of ad-hoc model mutation.
/// </summary>
public sealed class SetEntityPropertyCommand : IUndoableCommand
{
    private readonly ICadDocument _document;
    private readonly IEntity _entity;
    private readonly Action _apply;
    private readonly Action _revert;

    public SetEntityPropertyCommand(string name, ICadDocument document, IEntity entity, Action apply, Action revert)
    {
        Name = name;
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
        _apply = apply ?? throw new ArgumentNullException(nameof(apply));
        _revert = revert ?? throw new ArgumentNullException(nameof(revert));
    }

    public string Name { get; }

    public void Execute()
    {
        _apply();
        _document.NotifyEntityModified(_entity);
    }

    public void Undo()
    {
        _revert();
        _document.NotifyEntityModified(_entity);
    }
}
