using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Entities;

/// <summary>
/// Builds regular n-gons as closed polylines (#196). The polygon can be either inscribed in a
/// circle of the given radius (vertices on the circle) or circumscribed about it (edge midpoints
/// tangent to the circle). <paramref name="startAngle"/> orients the shape toward the cursor.
/// </summary>
public static class RegularPolygonFactory
{
    public const int MinSides = 3;

    public static IReadOnlyList<Point2D> Vertices(Point2D center, double radius, int sides, bool circumscribed, double startAngle = 0.0)
    {
        if (sides < MinSides)
            sides = MinSides;

        double step = 2.0 * Math.PI / sides;
        double circumradius = radius;
        double angle0 = startAngle;

        if (circumscribed)
        {
            // The cursor distance is the apothem (inradius); convert to the circumradius and rotate
            // by half a step so a flat edge — not a vertex — faces the cursor.
            circumradius = radius / Math.Cos(Math.PI / sides);
            angle0 = startAngle - step / 2.0;
        }

        var points = new List<Point2D>(sides);
        for (int i = 0; i < sides; i++)
        {
            double a = angle0 + i * step;
            points.Add(new Point2D(center.X + circumradius * Math.Cos(a), center.Y + circumradius * Math.Sin(a)));
        }

        return points;
    }

    public static PolylineEntity Create(Point2D center, double radius, int sides, bool circumscribed, double startAngle = 0.0)
        => new(Vertices(center, radius, sides, circumscribed, startAngle), isClosed: true);
}
