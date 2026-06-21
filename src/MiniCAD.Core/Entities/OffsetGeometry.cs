using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Entities;

/// <summary>
/// Produces a parallel copy of an entity that passes through a given "through" point — the
/// interactive form of the offset command, where the cursor decides both the side and the
/// distance. Returns <c>null</c> for entity kinds that cannot be offset or for a degenerate
/// (zero-distance) request.
/// </summary>
public static class OffsetGeometry
{
    public static IEntity? Offset(IEntity source, Point2D through) => source switch
    {
        LineEntity line => OffsetLine(line, through),
        CircleEntity circle => OffsetCircle(circle, through),
        ArcEntity arc => OffsetArc(arc, through),
        PolylineEntity polyline => OffsetPolyline(polyline, through),
        _ => null,
    };

    private static IEntity? OffsetLine(LineEntity line, Point2D through)
    {
        if (SignedOffset(line.Start, line.End, through) is not { } signed)
            return null;

        Vector2D offset = signed.Normal * signed.Distance;
        var result = (LineEntity)line.Clone();
        result.Start = line.Start + offset;
        result.End = line.End + offset;
        return result;
    }

    private static IEntity? OffsetCircle(CircleEntity circle, Point2D through)
    {
        double radius = circle.Center.DistanceTo(through);
        if (radius <= GeometryMath.Epsilon)
            return null;

        var result = (CircleEntity)circle.Clone();
        result.Radius = radius;
        return result;
    }

    private static IEntity? OffsetArc(ArcEntity arc, Point2D through)
    {
        double radius = arc.Center.DistanceTo(through);
        if (radius <= GeometryMath.Epsilon)
            return null;

        var result = (ArcEntity)arc.Clone();
        result.Radius = radius;
        return result;
    }

    private static IEntity? OffsetPolyline(PolylineEntity polyline, Point2D through)
    {
        IReadOnlyList<Point2D> points = polyline.Points;
        if (points.Count < 2)
            return null;

        int segmentCount = polyline.IsClosed ? points.Count : points.Count - 1;

        // The signed distance is taken from the segment nearest the cursor and then applied to
        // every segment along its own left normal, so the whole polyline shifts to one side.
        double distance = NearestSegmentSignedDistance(points, segmentCount, polyline.IsClosed, through);
        if (Math.Abs(distance) <= GeometryMath.Epsilon)
            return null;

        // Offset each segment as an infinite line, then meet consecutive lines at the new vertices.
        var offsetSegments = new (Point2D A, Point2D B)[segmentCount];
        for (int i = 0; i < segmentCount; i++)
        {
            Point2D p = points[i];
            Point2D q = points[(i + 1) % points.Count];
            Vector2D normal = LeftNormal(p, q);
            offsetSegments[i] = (p + normal * distance, q + normal * distance);
        }

        var result = new List<Point2D>(points.Count);
        if (polyline.IsClosed)
        {
            for (int i = 0; i < segmentCount; i++)
            {
                int prev = (i - 1 + segmentCount) % segmentCount;
                result.Add(MeetOrStart(offsetSegments[prev], offsetSegments[i]));
            }
        }
        else
        {
            result.Add(offsetSegments[0].A);
            for (int i = 1; i < segmentCount; i++)
                result.Add(MeetOrStart(offsetSegments[i - 1], offsetSegments[i]));
            result.Add(offsetSegments[^1].B);
        }

        var clone = (PolylineEntity)polyline.Clone();
        clone.RestoreState((result, polyline.IsClosed, polyline.SolidFill, new List<double>()));
        return clone;
    }

    /// <summary>Intersection of two offset segment-lines, falling back to the second line's start.</summary>
    private static Point2D MeetOrStart((Point2D A, Point2D B) first, (Point2D A, Point2D B) second)
        => GeometryMath.TryLineIntersection(first.A, first.B, second.A, second.B, out Point2D meet)
            ? meet
            : second.A;

    private static double NearestSegmentSignedDistance(
        IReadOnlyList<Point2D> points, int segmentCount, bool closed, Point2D through)
    {
        double bestDistance = double.MaxValue;
        double signed = 0.0;
        for (int i = 0; i < segmentCount; i++)
        {
            Point2D p = points[i];
            Point2D q = points[(i + 1) % points.Count];
            double d = GeometryMath.DistancePointToSegment(through, p, q, out _);
            if (d < bestDistance)
            {
                bestDistance = d;
                signed = (through - p).Dot(LeftNormal(p, q));
            }
        }

        _ = closed;
        return signed;
    }

    /// <summary>Signed perpendicular offset of <paramref name="through"/> from the line a→b.</summary>
    private static (Vector2D Normal, double Distance)? SignedOffset(Point2D a, Point2D b, Point2D through)
    {
        Vector2D direction = b - a;
        if (direction.LengthSquared <= GeometryMath.Epsilon)
            return null;

        Vector2D normal = direction.Perpendicular().Normalized();
        double distance = (through - a).Dot(normal);
        return Math.Abs(distance) <= GeometryMath.Epsilon ? null : (normal, distance);
    }

    /// <summary>Unit left normal of the directed segment a→b (90° CCW from its direction).</summary>
    private static Vector2D LeftNormal(Point2D a, Point2D b) => (b - a).Perpendicular().Normalized();
}
