using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Entities;

/// <summary>
/// Builds a donut/ring as two concentric circles (#196): the outer circle first, then the inner.
/// If the inner radius isn't strictly between 0 and the outer radius it is clamped to keep a
/// visible ring. The caller groups the two circles so they move as one.
/// </summary>
public static class DonutFactory
{
    public static IReadOnlyList<CircleEntity> Create(Point2D center, double innerRadius, double outerRadius)
    {
        double outer = Math.Abs(outerRadius);
        double inner = Math.Abs(innerRadius);
        if (inner <= 0.0 || inner >= outer)
            inner = outer * 0.6;

        return new[]
        {
            new CircleEntity(center, outer),
            new CircleEntity(center, inner),
        };
    }
}
