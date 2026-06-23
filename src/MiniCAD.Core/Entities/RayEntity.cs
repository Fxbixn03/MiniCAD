using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Entities;

/// <summary>A ray (RAY): a half-infinite construction line from the base point in one direction (#195).</summary>
public sealed class RayEntity : InfiniteLineEntity
{
    public RayEntity(Point2D start, double directionAngle)
        : base(start, directionAngle)
    {
    }

    private RayEntity(RayEntity source) : base(source)
    {
    }

    protected override (Point2D From, Point2D To) Extent() => (BasePoint, BasePoint + Dir * Far);

    public override bool HitTest(Point2D point, double tolerance)
    {
        // Behind the start: nearest point is the start itself.
        double along = (point - BasePoint).Dot(Dir);
        return along < 0
            ? BasePoint.DistanceTo(point) <= tolerance
            : PerpendicularDistance(point) <= tolerance;
    }

    public override IEntity Clone() => new RayEntity(this);
}
