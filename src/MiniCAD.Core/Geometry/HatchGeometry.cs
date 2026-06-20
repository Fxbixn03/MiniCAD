using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Geometry;

/// <summary>
/// Turns a hatch pattern into concrete line segments clipped to a closed polygon. The result
/// is a plain list of segments, which is all a renderer needs — hatching never reaches the
/// graphics backend as a special primitive. A scan-line/even-odd intersection test is used,
/// so both convex and concave polygons fill correctly.
/// </summary>
public static class HatchGeometry
{
    /// <summary>Safety cap so an absurdly small spacing at a huge extent can't lock up rendering.</summary>
    private const int MaxLinesPerFamily = 5000;

    /// <summary>Generates every clipped segment for all line families of <paramref name="pattern"/>.</summary>
    public static List<(Point2D A, Point2D B)> Generate(IReadOnlyList<Point2D> polygon, HatchPattern pattern)
        => Generate(polygon, pattern, spacingScale: 1.0);

    /// <summary>
    /// As <see cref="Generate(IReadOnlyList{Point2D}, HatchPattern)"/>, but scales every family's
    /// spacing/offset by <paramref name="spacingScale"/> (used for a Teilbild's reference scale,
    /// so the same pattern renders denser or coarser depending on 1:n).
    /// </summary>
    public static List<(Point2D A, Point2D B)> Generate(IReadOnlyList<Point2D> polygon, HatchPattern pattern, double spacingScale)
    {
        ArgumentNullException.ThrowIfNull(pattern);
        var segments = new List<(Point2D, Point2D)>();
        if (polygon.Count < 3)
            return segments;

        bool scaled = Math.Abs(spacingScale - 1.0) > GeometryMath.Epsilon && spacingScale > 0;
        foreach (HatchLineDefinition family in pattern.Lines)
        {
            HatchLineDefinition resolved = scaled
                ? family with { Spacing = family.Spacing * spacingScale, Offset = family.Offset * spacingScale }
                : family;
            Generate(polygon, resolved, segments);
        }

        return segments;
    }

    /// <summary>Generates the clipped segments for a single line family, appending to <paramref name="output"/>.</summary>
    public static void Generate(IReadOnlyList<Point2D> polygon, HatchLineDefinition family, List<(Point2D A, Point2D B)> output)
    {
        if (polygon.Count < 3 || family.Spacing <= GeometryMath.Epsilon)
            return;

        double angle = GeometryMath.DegreesToRadians(family.AngleDegrees);
        var dir = new Vector2D(Math.Cos(angle), Math.Sin(angle));
        Vector2D normal = dir.Perpendicular(); // unit normal: lines satisfy normal·X = t

        // Project the polygon onto the normal to find the band of line offsets we must cover.
        double minProj = double.PositiveInfinity;
        double maxProj = double.NegativeInfinity;
        foreach (Point2D vertex in polygon)
        {
            double proj = normal.X * vertex.X + normal.Y * vertex.Y;
            minProj = Math.Min(minProj, proj);
            maxProj = Math.Max(maxProj, proj);
        }

        double spacing = family.Spacing;
        // Anchor the family so it stays stable regardless of where the polygon sits.
        double firstK = Math.Ceiling((minProj - family.Offset) / spacing);
        double lastK = Math.Floor((maxProj - family.Offset) / spacing);
        if (lastK - firstK > MaxLinesPerFamily)
            return;

        var crossings = new List<double>();
        for (double k = firstK; k <= lastK; k++)
        {
            double t = family.Offset + k * spacing;
            crossings.Clear();
            CollectCrossings(polygon, normal, dir, t, crossings);
            if (crossings.Count < 2)
                continue;

            crossings.Sort();
            // A point on the line, then walk paired crossings (inside/outside spans).
            var basePoint = new Point2D(normal.X * t, normal.Y * t);
            for (int i = 0; i + 1 < crossings.Count; i += 2)
            {
                Point2D a = basePoint + dir * crossings[i];
                Point2D b = basePoint + dir * crossings[i + 1];
                output.Add((a, b));
            }
        }
    }

    private static void CollectCrossings(IReadOnlyList<Point2D> polygon, Vector2D normal, Vector2D dir, double t, List<double> crossings)
    {
        int count = polygon.Count;
        for (int i = 0; i < count; i++)
        {
            Point2D v0 = polygon[i];
            Point2D v1 = polygon[(i + 1) % count];

            double p0 = normal.X * v0.X + normal.Y * v0.Y;
            double p1 = normal.X * v1.X + normal.Y * v1.Y;
            double denom = p1 - p0;
            if (Math.Abs(denom) <= GeometryMath.Epsilon)
                continue; // edge parallel to the hatch line

            double u = (t - p0) / denom;
            // Half-open interval avoids counting a shared vertex twice.
            if (u < 0.0 || u >= 1.0)
                continue;

            Point2D hit = v0.Lerp(v1, u);
            crossings.Add(dir.X * hit.X + dir.Y * hit.Y);
        }
    }
}
