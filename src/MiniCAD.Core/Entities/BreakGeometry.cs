using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Entities;

/// <summary>
/// Splits an entity at a point or breaks the piece out between two points (#187). Split-at-point
/// turns a line/arc/polyline into two independent entities; break-between removes the middle
/// portion, leaving the surrounding pieces (a circle becomes the complementary arc). The results
/// inherit the source's layer/Teilbild/style; the caller swaps them in via undoable commands.
/// </summary>
public static class BreakGeometry
{
    private const double Eps = 1e-7;

    /// <summary>True if the entity supports breaking.</summary>
    public static bool CanBreak(IEntity entity)
        => entity is LineEntity or ArcEntity or CircleEntity or PolylineEntity;

    /// <summary>Splits the entity into two at <paramref name="at"/>; returns the original alone if it can't.</summary>
    public static IReadOnlyList<IEntity> SplitAt(IEntity entity, Point2D at)
    {
        switch (entity)
        {
            case LineEntity line:
            {
                double t = LineParam(line.Start, line.End, at);
                if (t <= Eps || t >= 1 - Eps)
                    return Single(entity);
                Point2D p = line.Start.Lerp(line.End, t);
                return Two(entity, new LineEntity(line.Start, p), new LineEntity(p, line.End));
            }

            case ArcEntity arc:
            {
                double rel = ArcParam(arc, at);
                if (Math.Abs(rel) <= Eps || Math.Abs(rel) >= Math.Abs(arc.SweepAngle) - Eps)
                    return Single(entity);
                var first = new ArcEntity(arc.Center, arc.Radius, arc.StartAngle, rel);
                var second = new ArcEntity(arc.Center, arc.Radius, arc.StartAngle + rel, arc.SweepAngle - rel);
                return Two(entity, first, second);
            }

            case PolylineEntity poly:
                return SplitPolyline(poly, at);

            default:
                return Single(entity); // a full circle has no single-point split
        }
    }

    /// <summary>Removes the portion of the entity between <paramref name="a"/> and <paramref name="b"/>.</summary>
    public static IReadOnlyList<IEntity> BreakBetween(IEntity entity, Point2D a, Point2D b)
    {
        switch (entity)
        {
            case LineEntity line:
            {
                double ta = LineParam(line.Start, line.End, a);
                double tb = LineParam(line.Start, line.End, b);
                if (ta > tb)
                    (ta, tb) = (tb, ta);

                var pieces = new List<IEntity>();
                if (ta > Eps)
                    pieces.Add(Style(new LineEntity(line.Start, line.Start.Lerp(line.End, ta)), entity));
                if (tb < 1 - Eps)
                    pieces.Add(Style(new LineEntity(line.Start.Lerp(line.End, tb), line.End), entity));
                return pieces;
            }

            case CircleEntity circle:
            {
                double aAng = Angle(circle.Center, a);
                double bAng = Angle(circle.Center, b);
                // Remove the CCW arc a→b; keep the remaining arc b→a (CCW).
                double sweep = GeometryMath.NormalizeAngle(aAng - bAng);
                return new List<IEntity> { Style(new ArcEntity(circle.Center, circle.Radius, bAng, sweep), entity) };
            }

            case ArcEntity arc:
            {
                double ra = ArcParam(arc, a);
                double rb = ArcParam(arc, b);
                if (Math.Abs(ra) > Math.Abs(rb))
                    (ra, rb) = (rb, ra);

                var pieces = new List<IEntity>();
                if (Math.Abs(ra) > Eps)
                    pieces.Add(Style(new ArcEntity(arc.Center, arc.Radius, arc.StartAngle, ra), entity));
                if (Math.Abs(rb) < Math.Abs(arc.SweepAngle) - Eps)
                    pieces.Add(Style(new ArcEntity(arc.Center, arc.Radius, arc.StartAngle + rb, arc.SweepAngle - rb), entity));
                return pieces;
            }

            case PolylineEntity { IsClosed: false } poly:
                return BreakPolyline(poly, a, b);

            default:
                return Single(entity);
        }
    }

    // ----- Polyline helpers -----

    private static IReadOnlyList<IEntity> SplitPolyline(PolylineEntity poly, Point2D at)
    {
        if (poly.Points.Count < 2)
            return Single(poly);

        (int seg, double t) = NearestSegment(poly, at);
        var pts = poly.Points;
        Point2D sp = pts[seg].Lerp(pts[(seg + 1) % pts.Count], t);

        if (poly.IsClosed)
        {
            // Opening a closed polyline at the break point: walk all the way round back to it.
            var chain = new List<Point2D> { sp };
            for (int k = 0; k < pts.Count; k++)
                chain.Add(pts[(seg + 1 + k) % pts.Count]);
            chain.Add(sp);
            return Single(Style(new PolylineEntity(Dedupe(chain), isClosed: false), poly));
        }

        var first = new List<Point2D>();
        for (int i = 0; i <= seg; i++)
            first.Add(pts[i]);
        first.Add(sp);

        var second = new List<Point2D> { sp };
        for (int i = seg + 1; i < pts.Count; i++)
            second.Add(pts[i]);

        if (Dedupe(first).Count < 2 || Dedupe(second).Count < 2)
            return Single(poly);

        return Two(poly, new PolylineEntity(Dedupe(first)), new PolylineEntity(Dedupe(second)));
    }

    private static IReadOnlyList<IEntity> BreakPolyline(PolylineEntity poly, Point2D a, Point2D b)
    {
        if (poly.Points.Count < 2)
            return Single(poly);

        (int segA, double tA) = NearestSegment(poly, a);
        (int segB, double tB) = NearestSegment(poly, b);
        if (segB < segA || (segA == segB && tB < tA))
        {
            (segA, segB) = (segB, segA);
            (tA, tB) = (tB, tA);
        }

        var pts = poly.Points;
        Point2D pa = pts[segA].Lerp(pts[segA + 1], tA);
        Point2D pb = pts[segB].Lerp(pts[segB + 1], tB);

        var first = new List<Point2D>();
        for (int i = 0; i <= segA; i++)
            first.Add(pts[i]);
        first.Add(pa);

        var second = new List<Point2D> { pb };
        for (int i = segB + 1; i < pts.Count; i++)
            second.Add(pts[i]);

        var pieces = new List<IEntity>();
        if (Dedupe(first).Count >= 2)
            pieces.Add(Style(MakePolylineOrLine(Dedupe(first)), poly));
        if (Dedupe(second).Count >= 2)
            pieces.Add(Style(MakePolylineOrLine(Dedupe(second)), poly));
        return pieces;
    }

    private static (int Segment, double T) NearestSegment(PolylineEntity poly, Point2D point)
    {
        var pts = poly.Points;
        int count = poly.IsClosed ? pts.Count : pts.Count - 1;
        int best = 0;
        double bestDist = double.MaxValue;
        double bestT = 0;

        for (int i = 0; i < count; i++)
        {
            Point2D s = pts[i], e = pts[(i + 1) % pts.Count];
            double d = GeometryMath.DistancePointToSegment(point, s, e, out _);
            if (d < bestDist)
            {
                bestDist = d;
                best = i;
                bestT = LineParam(s, e, point);
            }
        }

        return (best, bestT);
    }

    // ----- Shared helpers -----

    private static double LineParam(Point2D start, Point2D end, Point2D p)
    {
        Vector2D dir = end - start;
        double lenSq = dir.LengthSquared;
        if (lenSq <= Eps)
            return 0;
        double t = ((p.X - start.X) * dir.X + (p.Y - start.Y) * dir.Y) / lenSq;
        return Math.Clamp(t, 0, 1);
    }

    /// <summary>Signed angular offset of <paramref name="point"/> from the arc start, along its sweep.</summary>
    private static double ArcParam(ArcEntity arc, Point2D point)
    {
        double angle = Angle(arc.Center, point);
        if (arc.SweepAngle >= 0)
            return GeometryMath.NormalizeAngle(angle - arc.StartAngle);
        return -GeometryMath.NormalizeAngle(arc.StartAngle - angle);
    }

    private static double Angle(Point2D center, Point2D point)
        => Math.Atan2(point.Y - center.Y, point.X - center.X);

    private static List<Point2D> Dedupe(List<Point2D> points)
    {
        var result = new List<Point2D>(points.Count);
        foreach (Point2D p in points)
            if (result.Count == 0 || result[^1].DistanceTo(p) > Eps)
                result.Add(p);
        return result;
    }

    private static IEntity MakePolylineOrLine(List<Point2D> points)
        => points.Count == 2 ? new LineEntity(points[0], points[1]) : new PolylineEntity(points);

    private static IReadOnlyList<IEntity> Single(IEntity entity) => new[] { entity };

    private static IReadOnlyList<IEntity> Two(IEntity source, IEntity first, IEntity second)
        => new[] { Style(first, source), Style(second, source) };

    private static TEntity Style<TEntity>(TEntity entity, IEntity source) where TEntity : IEntity
    {
        entity.LayerId = source.LayerId;
        entity.PartialDrawingId = source.PartialDrawingId;
        entity.StrokeOverride = source.StrokeOverride;
        entity.IsConstruction = source.IsConstruction;
        return entity;
    }
}
