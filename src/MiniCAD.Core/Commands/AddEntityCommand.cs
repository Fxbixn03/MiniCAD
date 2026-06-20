using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;

namespace MiniCAD.Core.Commands;

/// <summary>Adds an entity to a document; undo removes it again.</summary>
public sealed class AddEntityCommand : IUndoableCommand
{
    private readonly ICadDocument _document;
    private readonly IEntity _entity;

    public AddEntityCommand(ICadDocument document, IEntity entity)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _entity = entity ?? throw new ArgumentNullException(nameof(entity));
    }

    public string Name => $"Add {_entity.GetType().Name}";

    public void Execute() => _document.AddEntity(_entity);

    public void Undo() => _document.RemoveEntity(_entity);
}
