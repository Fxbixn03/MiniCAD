using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Entities;

/// <summary>
/// Computes how two lines should be joined by a fillet (tangent arc) or a chamfer (bevel line).
/// Each line keeps the end on the side it was picked and is trimmed/extended to the tangent
/// point; a distance of 0 produces a sharp corner at the intersection. The caller applies the
/// result (re-points the two lines and adds the optional connector).
/// </summary>
public static class FilletGeometry
{
    /// <summary>The new geometry: the re-pointed ends of both lines plus an optional connector.</summary>
    public readonly record struct Result(
        Point2D Keep1, Point2D Tangent1,
        Point2D Keep2, Point2D Tangent2,
        IEntity? Connector);

    /// <summary>
    /// Joins <paramref name="line1"/> (picked near <paramref name="pick1"/>) and
    /// <paramref name="line2"/> (picked near <paramref name="pick2"/>). <paramref name="distance"/>
    /// is the fillet radius, or the chamfer setback when <paramref name="chamfer"/> is true.
    /// Returns <c>null</c> for parallel or collinear lines.
    /// </summary>
    public static Result? Compute(LineEntity line1, Point2D pick1, LineEntity line2, Point2D pick2, double distance, bool chamfer)
    {
        if (!GeometryMath.TryLineIntersection(line1.Start, line1.End, line2.Start, line2.End, out Point2D corner))
            return null;

        Vector2D u1 = KeptDirection(line1, corner, pick1);
        Vector2D u2 = KeptDirection(line2, corner, pick2);
        if (u1 == Vector2D.Zero || u2 == Vector2D.Zero)
            return null;

        double cosTheta = GeometryMath.Clamp(u1.Dot(u2), -1.0, 1.0);
        double theta = Math.Acos(cosTheta);
        if (theta <= GeometryMath.Epsilon || theta >= Math.PI - GeometryMath.Epsilon)
            return null; // collinear: nothing meaningful to round

        Point2D keep1 = FarthestEndpoint(line1, corner, u1);
        Point2D keep2 = FarthestEndpoint(line2, corner, u2);

        double setback = chamfer ? distance : distance / Math.Tan(theta / 2.0);
        Point2D tangent1 = corner + u1 * setback;
        Point2D tangent2 = corner + u2 * setback;

        IEntity? connector = null;
        if (chamfer)
        {
            if (distance > GeometryMath.Epsilon)
                connector = CopyStyle(line1, new LineEntity(tangent1, tangent2));
        }
        else if (distance > GeometryMath.Epsilon)
        {
            connector = BuildArc(line1, corner, u1, u2, tangent1, tangent2, distance, theta);
        }

        return new Result(keep1, tangent1, keep2, tangent2, connector);
    }

    private static IEntity BuildArc(
        LineEntity style, Point2D corner, Vector2D u1, Vector2D u2,
        Point2D tangent1, Point2D tangent2, double radius, double theta)
    {
        Vector2D bisector = (u1 + u2).Normalized();
        Point2D center = corner + bisector * (radius / Math.Sin(theta / 2.0));

        double start = Math.Atan2(tangent1.Y - center.Y, tangent1.X - center.X);
        double end = Math.Atan2(tangent2.Y - center.Y, tangent2.X - center.X);
        double sweep = end - start;
        while (sweep > Math.PI) sweep -= Math.PI * 2.0;
        while (sweep <= -Math.PI) sweep += Math.PI * 2.0;

        return CopyStyle(style, new ArcEntity(center, radius, start, sweep));
    }

    /// <summary>Unit direction from <paramref name="corner"/> toward the picked side of the line.</summary>
    private static Vector2D KeptDirection(LineEntity line, Point2D corner, Point2D pick)
    {
        Vector2D direction = (line.End - line.Start).Normalized();
        if (direction == Vector2D.Zero)
            return Vector2D.Zero;

        return (pick - corner).Dot(direction) >= 0 ? direction : -direction;
    }

    private static Point2D FarthestEndpoint(LineEntity line, Point2D corner, Vector2D keptDirection)
        => (line.Start - corner).Dot(keptDirection) >= (line.End - corner).Dot(keptDirection)
            ? line.Start
            : line.End;

    private static TEntity CopyStyle<TEntity>(IEntity source, TEntity target) where TEntity : IEntity
    {
        target.LayerId = source.LayerId;
        target.PartialDrawingId = source.PartialDrawingId;
        target.StrokeOverride = source.StrokeOverride;
        return target;
    }
}
