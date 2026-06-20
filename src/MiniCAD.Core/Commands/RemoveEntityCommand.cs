using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;

namespace MiniCAD.Core.Commands;

/// <summary>Removes an entity from a document; undo restores it.</summary>
public sealed class RemoveEntityCommand : IUndoableCommand
{
    private readonly ICadDocument _document;
    private readonly IEntity _entity;

    public RemoveEntityCommand(ICadDocument document, IEntity entity)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }

    public string Name => $"Remove {_entity.GetType().Name}";

    public void Execute() => _document.RemoveEntity(_entity);

    public void Undo() => _document.AddEntity(_entity);
}
