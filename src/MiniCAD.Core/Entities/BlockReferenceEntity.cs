using System.Linq;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// An instance of a <see cref="BlockDefinition"/> placed at an insertion point with a uniform
/// scale and rotation. Rendering, hit-testing, snapping and bounds all delegate to the block's
/// geometry under the instance transform. The definition is linked in memory (resolved from the
/// document on load); only the id is persisted. Per-instance <see cref="Attributes"/> hold
/// labelled field values for later evaluation.
/// </summary>
public sealed class BlockReferenceEntity : Entity, IEditableEntity
{
    public BlockReferenceEntity(BlockDefinition definition, Point2D position, double scale = 1.0, double rotation = 0.0)
        : this(definition?.Id ?? Guid.Empty, position, scale, rotation)
    {
        Definition = definition;
    }

    public BlockReferenceEntity(Guid definitionId, Point2D position, double scale = 1.0, double rotation = 0.0)
    {
        DefinitionId = definitionId;
        Position = position;
        Scale = scale;
        Rotation = rotation;
    }

    private BlockReferenceEntity(BlockReferenceEntity source) : base(source)
    {
        DefinitionId = source.DefinitionId;
        Position = source.Position;
        Scale = source.Scale;
        Rotation = source.Rotation;
        Definition = source.Definition;
        foreach ((string key, string value) in source.Attributes)
            Attributes[key] = value;
    }

    public Guid DefinitionId { get; private set; }

    /// <summary>The linked definition (resolved in memory); <c>null</c> until linked.</summary>
    public BlockDefinition? Definition { get; set; }

    public Point2D Position { get; set; }

    public double Scale { get; set; } = 1.0;

    /// <summary>Rotation in radians, CCW.</summary>
    public double Rotation { get; set; }

    /// <summary>Per-instance attribute values (e.g. "Raumname" → "Büro").</summary>
    public Dictionary<string, string> Attributes { get; } = new();

    /// <summary>Maps block-definition coordinates to world: base point → insertion point.</summary>
    private Matrix2D InstanceMatrix
    {
        get
        {
            Point2D basePoint = Definition?.BasePoint ?? Position;
            return Matrix2D.Translation(-basePoint.X, -basePoint.Y)
                * Matrix2D.Scaling(Scale, Scale)
                * Matrix2D.Rotation(Rotation)
                * Matrix2D.Translation(Position.X, Position.Y);
        }
    }

    /// <summary>The definition's entities cloned and placed under the instance transform.</summary>
    private IEnumerable<IEntity> Instantiate()
    {
        if (Definition is null)
            yield break;

        Matrix2D matrix = InstanceMatrix;
        foreach (IEntity entity in Definition.Entities)
        {
            IEntity clone = entity.Clone();
            clone.Transform(matrix);
            yield return clone;
        }
    }

    public override Rect2D Bounds
    {
        get
        {
            Rect2D? bounds = null;
            foreach (IEntity child in Instantiate())
                bounds = bounds is { } b ? b.Union(child.Bounds) : child.Bounds;
            return bounds ?? Rect2D.FromCenter(Position, 0, 0);
        }
    }

    public override IEnumerable<Point2D> SnapPoints
    {
        get
        {
            yield return Position;
            foreach (IEntity child in Instantiate())
                foreach (Point2D p in child.SnapPoints)
                    yield return p;
        }
    }

    public override bool HitTest(Point2D point, double tolerance)
        => Instantiate().Any(child => child.HitTest(point, tolerance));

    public override bool IntersectsRect(Rect2D rect)
        => Instantiate().Any(child => child.IntersectsRect(rect));

    public override void Transform(in Matrix2D matrix)
    {
        var axis = new Vector2D(Math.Cos(Rotation), Math.Sin(Rotation));
        Vector2D transformedAxis = matrix.TransformVector(axis);

        Position = matrix.Transform(Position);
        Scale *= matrix.UniformScale;
        Rotation = Math.Atan2(transformedAxis.Y, transformedAxis.X);
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        foreach (IEntity child in Instantiate())
            child.Render(surface, child.StrokeOverride ?? stroke); // baked override, else ByBlock
    }

    public override IEntity Clone() => new BlockReferenceEntity(this);

    // ----- Direct editing -----

    public IReadOnlyList<Grip> GetGrips() => new[] { new Grip(Position, GripKind.Vertex, 0) };

    public void MoveGrip(Grip grip, Point2D newPosition) => Position = newPosition;

    public object CaptureState() => (Position, Scale, Rotation);

    public void RestoreState(object state) => (Position, Scale, Rotation) = ((Point2D, double, double))state;
}
