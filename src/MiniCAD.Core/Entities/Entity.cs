using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// Base class that handles the identity/layer/style bookkeeping shared by all entities,
/// leaving subclasses to provide only their geometry-specific behaviour.
/// </summary>
public abstract class Entity : IEntity
{
    protected Entity() => Id = EntityId.New();

    /// <summary>Copy constructor used by <see cref="Clone"/>; assigns a fresh <see cref="Id"/>.</summary>
    protected Entity(Entity source)
    {
        Id = EntityId.New();
        LayerId = source.LayerId;
        PartialDrawingId = source.PartialDrawingId;
        StrokeOverride = source.StrokeOverride;
        IsConstruction = source.IsConstruction;
    }

    public EntityId Id { get; }

    public Guid LayerId { get; set; }

    public Guid PartialDrawingId { get; set; }

    public StrokeStyle? StrokeOverride { get; set; }

    public bool IsConstruction { get; set; }

    public abstract Rect2D Bounds { get; }

    public abstract IEnumerable<Point2D> SnapPoints { get; }

    public abstract bool HitTest(Point2D point, double tolerance);

    public abstract bool IntersectsRect(Rect2D rect);

    public abstract void Transform(in Matrix2D matrix);

    public abstract void Render(IRenderSurface surface, in StrokeStyle stroke);

    public abstract IEntity Clone();
}
