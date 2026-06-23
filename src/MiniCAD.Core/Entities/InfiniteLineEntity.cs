using MiniCAD.Core.Geometry;
using MiniCAD.Core.Rendering;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>
/// Marker for entities of unbounded extent (construction lines / rays, #195). They are excluded
/// from "zoom to content" so an infinite line never blows up the drawing bounds.
/// </summary>
public interface IUnboundedEntity
{
}

/// <summary>
/// Shared base for the infinite construction line and the ray (#195): a base point plus a
/// direction. Both are "zoom-independent infinite" — drawn as a very long line, but hit-tested and
/// snapped against the true (semi-)infinite geometry, so behaviour doesn't change with zoom.
/// </summary>
public abstract class InfiniteLineEntity : Entity, IEditableEntity, IUnboundedEntity
{
    /// <summary>How far the line is extended for drawing/extent (world units).</summary>
    protected const double Far = 1_000_000.0;

    private const double DirectionGripReach = 100.0;

    protected InfiniteLineEntity(Point2D basePoint, double directionAngle)
    {
        BasePoint = basePoint;
        DirectionAngle = directionAngle;
    }

    protected InfiniteLineEntity(InfiniteLineEntity source) : base(source)
    {
        BasePoint = source.BasePoint;
        DirectionAngle = source.DirectionAngle;
    }

    /// <summary>The through point (line) or start point (ray).</summary>
    public Point2D BasePoint { get; set; }

    /// <summary>Direction in radians, CCW from +X.</summary>
    public double DirectionAngle { get; set; }

    protected Vector2D Dir => new(Math.Cos(DirectionAngle), Math.Sin(DirectionAngle));

    /// <summary>The drawn extent: a long segment spanning the visible geometry.</summary>
    protected abstract (Point2D From, Point2D To) Extent();

    public override Rect2D Bounds
    {
        get
        {
            (Point2D from, Point2D to) = Extent();
            return Rect2D.FromPoints(from, to);
        }
    }

    public override IEnumerable<Point2D> SnapPoints
    {
        get { yield return BasePoint; }
    }

    public override bool IntersectsRect(Rect2D rect)
    {
        if (rect.Contains(BasePoint))
            return true;

        (Point2D from, Point2D to) = Extent();
        return GeometryMath.SegmentIntersectsRect(from, to, rect);
    }

    public override void Transform(in Matrix2D matrix)
    {
        Vector2D transformed = matrix.TransformVector(Dir);
        BasePoint = matrix.Transform(BasePoint);
        if (transformed.Length > GeometryMath.Epsilon)
            DirectionAngle = Math.Atan2(transformed.Y, transformed.X);
    }

    public override void Render(IRenderSurface surface, in StrokeStyle stroke)
    {
        (Point2D from, Point2D to) = Extent();
        surface.DrawLine(from, to, stroke);
    }

    /// <summary>Perpendicular distance from <paramref name="point"/> to the infinite line through the base.</summary>
    protected double PerpendicularDistance(Point2D point)
    {
        Vector2D dir = Dir;
        Vector2D v = point - BasePoint;
        return Math.Abs(v.X * dir.Y - v.Y * dir.X); // |v × dir|, dir is a unit vector
    }

    public IReadOnlyList<Grip> GetGrips() => new[]
    {
        new Grip(BasePoint, GripKind.Vertex, 0),
        new Grip(BasePoint + Dir * DirectionGripReach, GripKind.Vertex, 1),
    };

    public void MoveGrip(Grip grip, Point2D newPosition)
    {
        if (grip.Index == 0)
        {
            BasePoint = newPosition;
        }
        else
        {
            Vector2D d = newPosition - BasePoint;
            if (d.Length > GeometryMath.Epsilon)
                DirectionAngle = Math.Atan2(d.Y, d.X);
        }
    }

    public object CaptureState() => (BasePoint, DirectionAngle);

    public void RestoreState(object state) => (BasePoint, DirectionAngle) = ((Point2D, double))state;
}
