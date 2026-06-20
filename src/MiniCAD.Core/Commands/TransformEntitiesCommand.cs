using MiniCAD.Core.Documents;
using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Commands;

/// <summary>
/// Applies an affine transform (move, rotate, scale) to a set of entities. Undo applies the
/// inverse transform, so the matrix must be invertible.
/// </summary>
public sealed class TransformEntitiesCommand : IUndoableCommand
{
    private readonly ICadDocument _document;
    private readonly IReadOnlyList<IEntity> _targets;
    private readonly Matrix2D _transform;
    private readonly Matrix2D _inverse;

    public TransformEntitiesCommand(ICadDocument document, IReadOnlyList<IEntity> targets, Matrix2D transform)
    {
        _document = document ?? throw new ArgumentNullException(nameof(document));
        _targets = targets ?? throw new ArgumentNullException(nameof(targets));

        if (!transform.TryInvert(out _inverse))
            throw new ArgumentException("Transform must be invertible to be undoable.", nameof(transform));

        _transform = transform;
    }

    public string Name => "Transform";

    public void Execute() => Apply(_transform);

    public void Undo() => Apply(_inverse);

    private void Apply(in Matrix2D matrix)
    {
        foreach (IEntity entity in _targets)
        {
            entity.Transform(matrix);
            _document.NotifyEntityModified(entity);
        }
    }
}
