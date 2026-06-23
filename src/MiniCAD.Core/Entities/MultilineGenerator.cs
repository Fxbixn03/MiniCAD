using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Entities;

/// <summary>Which line of a double/parallel run the picked points represent (#236).</summary>
public enum MultilineJustification
{
    /// <summary>The picked path is the centerline; rails sit ±width/2 to each side.</summary>
    Center,

    /// <summary>The picked path is the left rail; the second rail is one width to the right.</summary>
    Left,

    /// <summary>The picked path is the right rail; the second rail is one width to the left.</summary>
    Right,
}

/// <summary>
/// Builds a double/parallel line run (Allplan "Doppellinie", #236): two polylines parallel to a
/// picked path at a given width, with mitered corners. The justification places the path on the
/// centerline or on either rail. Both rails share the path's vertex count.
/// </summary>
public static class MultilineGenerator
{
    public static IReadOnlyList<PolylineEntity> Generate(
        IReadOnlyList<Point2D> path, bool closed, double width, MultilineJustification justification)
    {
        ArgumentNullException.ThrowIfNull(path);

        (double offsetA, double offsetB) = justification switch
        {
            MultilineJustification.Left => (0.0, -width),
            MultilineJustification.Right => (width, 0.0),
            _ => (width / 2.0, -width / 2.0),
        };

        return new[]
        {
            new PolylineEntity(OffsetPath(path, closed, offsetA), closed),
            new PolylineEntity(OffsetPath(path, closed, offsetB), closed),
        };
    }

    /// <summary>
    /// Offsets a polyline to the left of travel by <paramref name="distance"/> (negative = right),
    /// mitering interior corners so the two offset segments meet at a point.
    /// </summary>
    internal static IReadOnlyList<Point2D> OffsetPath(IReadOnlyList<Point2D> path, bool closed, double distance)
    {
        int n = path.Count;
        var result = new List<Point2D>(n);
        if (n == 0)
            return result;
        if (n == 1)
        {
            result.Add(path[0]);
            return result;
        }

        for (int i = 0; i < n; i++)
        {
            Vector2D? incoming = SegmentNormal(path, i - 1, i, closed);
            Vector2D? outgoing = SegmentNormal(path, i, i + 1, closed);

            if (incoming is { } a && outgoing is { } b)
            {
                // Miter: the offset vertex lies along the averaged normal, scaled so both offset
                // edges pass through it. Fall back to a simple offset at sharp/degenerate corners.
                Vector2D mitre = (a + b);
                if (mitre.LengthSquared <= GeometryMath.Epsilon)
                {
                    result.Add(path[i] + a * distance);
                    continue;
                }

                mitre = mitre.Normalized();
                double cos = mitre.Dot(a);
                if (Math.Abs(cos) <= 1e-6)
                    result.Add(path[i] + a * distance);
                else
                    result.Add(path[i] + mitre * (distance / cos));
            }
            else if (incoming is { } onlyIn)
            {
                result.Add(path[i] + onlyIn * distance);
            }
            else if (outgoing is { } onlyOut)
            {
                result.Add(path[i] + onlyOut * distance);
            }
            else
            {
                result.Add(path[i]);
            }
        }

        return result;
    }

    /// <summary>The unit left normal of the segment between two vertex indices, or null if degenerate.</summary>
    private static Vector2D? SegmentNormal(IReadOnlyList<Point2D> path, int from, int to, bool closed)
    {
        int n = path.Count;
        if (closed)
        {
            from = (from + n) % n;
            to = (to + n) % n;
        }
        else if (from < 0 || to >= n)
        {
            return null;
        }

        Vector2D dir = path[to] - path[from];
        if (dir.Length <= GeometryMath.Epsilon)
            return null;

        return dir.Normalized().Perpendicular(); // left of travel (Y up)
    }
}
