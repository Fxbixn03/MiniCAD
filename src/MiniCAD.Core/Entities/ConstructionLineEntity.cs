using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Entities;

/// <summary>An infinite construction line (XLINE): extends both ways through the base point (#195).</summary>
public sealed class ConstructionLineEntity : InfiniteLineEntity
{
    public ConstructionLineEntity(Point2D through, double directionAngle)
        : base(through, directionAngle)
    {
    }

    private ConstructionLineEntity(ConstructionLineEntity source) : base(source)
    {
    }

    protected override (Point2D From, Point2D To) Extent()
    {
        Vector2D reach = Dir * Far;
        return (BasePoint - reach, BasePoint + reach);
    }

    public override bool HitTest(Point2D point, double tolerance) => PerpendicularDistance(point) <= tolerance;

    public override IEntity Clone() => new ConstructionLineEntity(this);
}
