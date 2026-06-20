using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Entities;

/// <summary>
/// Computes the points where two entities cross. Each entity is decomposed into straight
/// segments and circular arcs, and every primitive pair is intersected. This is the geometric
/// backbone of the trim/extend tool (which other objects cut a target, and where).
/// </summary>
public static class EntityIntersection
{
    private readonly record struct Segment(Point2D A, Point2D B);

    private readonly record struct Arc(Point2D Center, double Radius, double Start, double Sweep);

    /// <summary>All points where <paramref name="a"/> and <paramref name="b"/> intersect.</summary>
    public static List<Point2D> Between(IEntity a, IEntity b)
    {
        var (segmentsA, arcsA) = Decompose(a);
        var (segmentsB, arcsB) = Decompose(b);
        var results = new List<Point2D>();

        foreach (Segment s in segmentsA)
        {
            foreach (Segment t in segmentsB)
            {
                if (GeometryMath.IntersectSegments(s.A, s.B, t.A, t.B, out Point2D point))
                    AddUnique(results, point);
            }

            foreach (Arc arc in arcsB)
                AddSegmentArc(results, s, arc);
        }

        foreach (Arc arc in arcsA)
        {
            foreach (Segment t in segmentsB)
                AddSegmentArc(results, t, arc);

            foreach (Arc other in arcsB)
                AddArcArc(results, arc, other);
        }

        return results;
    }

    private static void AddSegmentArc(List<Point2D> results, Segment segment, Arc arc)
    {
        var candidates = new List<Point2D>(2);
        GeometryMath.IntersectSegmentCircle(segment.A, segment.B, arc.Center, arc.Radius, candidates);
        foreach (Point2D point in candidates)
        {
            if (OnArc(arc, point))
                AddUnique(results, point);
        }
    }

    private static void AddArcArc(List<Point2D> results, Arc first, Arc second)
    {
        var candidates = new List<Point2D>(2);
        GeometryMath.IntersectCircles(first.Center, first.Radius, second.Center, second.Radius, candidates);
        foreach (Point2D point in candidates)
        {
            if (OnArc(first, point) && OnArc(second, point))
                AddUnique(results, point);
        }
    }

    private static bool OnArc(Arc arc, Point2D point)
    {
        double angle = Math.Atan2(point.Y - arc.Center.Y, point.X - arc.Center.X);
        return GeometryMath.AngleInSweep(arc.Start, arc.Sweep, angle);
    }

    private static void AddUnique(List<Point2D> results, Point2D point)
    {
        foreach (Point2D existing in results)
        {
            if (existing.DistanceSquaredTo(point) <= GeometryMath.Epsilon)
                return;
        }

        results.Add(point);
    }

    private static (List<Segment> Segments, List<Arc> Arcs) Decompose(IEntity entity)
    {
        var segments = new List<Segment>();
        var arcs = new List<Arc>();

        switch (entity)
        {
            case LineEntity line:
                segments.Add(new Segment(line.Start, line.End));
                break;

            case PolylineEntity polyline:
                IReadOnlyList<Point2D> points = polyline.Points;
                for (int i = 0; i < points.Count - 1; i++)
                    segments.Add(new Segment(points[i], points[i + 1]));
                if (polyline.IsClosed && points.Count > 2)
                    segments.Add(new Segment(points[^1], points[0]));
                break;

            case CircleEntity circle:
                arcs.Add(new Arc(circle.Center, circle.Radius, 0, Math.PI * 2.0));
                break;

            case ArcEntity arc:
                arcs.Add(new Arc(arc.Center, arc.Radius, arc.StartAngle, arc.SweepAngle));
                break;
        }

        return (segments, arcs);
    }
}
