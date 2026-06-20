using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Entities;

/// <summary>
/// The geometric core of the trim/extend tool. Trimming removes the span of a target that sits
/// between the two cutting intersections bracketing the pick point; extending grows the picked
/// end of an open target out to the nearest boundary. Lines and polylines are handled as paths;
/// circles and arcs are handled in angle space. The caller supplies the cut/boundary geometry.
/// </summary>
public static class TrimGeometry
{
    private const double Tol = 1e-6;

    /// <summary>
    /// Trims <paramref name="target"/> at <paramref name="cutPoints"/>, removing the piece under
    /// <paramref name="pick"/>. Returns the replacement entities (0–2), or <c>null</c> when there
    /// is nothing to trim. A circle becomes an arc; a line/polyline may split in two.
    /// </summary>
    public static IReadOnlyList<IEntity>? Trim(IEntity target, Point2D pick, IReadOnlyList<Point2D> cutPoints)
        => target switch
        {
            LineEntity or PolylineEntity => TrimPath(target, pick, cutPoints),
            CircleEntity circle => TrimCircle(circle, pick, cutPoints),
            ArcEntity arc => TrimArc(arc, pick, cutPoints),
            _ => null,
        };

    /// <summary>
    /// Extends the picked end of an open <paramref name="target"/> (line or polyline) to the
    /// nearest of <paramref name="cutters"/> along the end segment's direction. Returns a modified
    /// clone or <c>null</c> if nothing lies ahead.
    /// </summary>
    public static IEntity? Extend(IEntity target, Point2D pick, IReadOnlyList<IEntity> cutters)
        => target switch
        {
            LineEntity or PolylineEntity => ExtendPath(target, pick, cutters),
            _ => null,
        };

    // ----- Path (line / polyline) -----

    private static IReadOnlyList<IEntity>? TrimPath(IEntity target, Point2D pick, IReadOnlyList<Point2D> cutPoints)
    {
        var path = Path.From(target);
        var cuts = new List<double>();
        foreach (Point2D point in cutPoints)
        {
            double position = path.PositionOf(point, out double distance);
            if (distance <= Tol)
                cuts.Add(position);
        }

        cuts.Sort();
        if (cuts.Count == 0 || (path.Closed && cuts.Count < 2))
            return null;

        double clickPos = path.PositionOf(pick, out _);
        var pieces = new List<IReadOnlyList<Point2D>>();

        if (!path.Closed)
        {
            double lower = 0.0;
            double upper = path.Length;
            foreach (double cut in cuts)
            {
                if (cut <= clickPos + Tol)
                    lower = cut;
            }

            for (int i = cuts.Count - 1; i >= 0; i--)
            {
                if (cuts[i] >= clickPos - Tol)
                    upper = cuts[i];
            }

            if (lower > Tol)
                pieces.Add(path.Slice(0.0, lower));
            if (upper < path.Length - Tol)
                pieces.Add(path.Slice(upper, path.Length));
        }
        else
        {
            // Find the gap between consecutive cuts (cyclically) that contains the pick.
            int count = cuts.Count;
            for (int i = 0; i < count; i++)
            {
                double lo = cuts[i];
                double hi = i + 1 < count ? cuts[i + 1] : cuts[0] + path.Length;
                double click = clickPos < lo ? clickPos + path.Length : clickPos;
                if (click >= lo - Tol && click <= hi + Tol)
                {
                    // Keep the complement: forward from hi around to lo.
                    pieces.Add(path.SliceForward(hi % path.Length, lo));
                    break;
                }
            }
        }

        if (pieces.Count == 0)
            return null;

        var result = new List<IEntity>(pieces.Count);
        foreach (IReadOnlyList<Point2D> piece in pieces)
        {
            if (piece.Count >= 2)
                result.Add(BuildPath(target, piece));
        }

        return result.Count == 0 ? null : result;
    }

    private static IEntity? ExtendPath(IEntity target, Point2D pick, IReadOnlyList<IEntity> cutters)
    {
        var path = Path.From(target);
        if (path.Closed || path.Vertices.Count < 2)
            return null;

        // Pick whichever free end is nearer the click, and the direction it points outward.
        bool atStart = pick.DistanceSquaredTo(path.Vertices[0]) <= pick.DistanceSquaredTo(path.Vertices[^1]);
        Point2D end = atStart ? path.Vertices[0] : path.Vertices[^1];
        Point2D inner = atStart ? path.Vertices[1] : path.Vertices[^2];
        Vector2D direction = (end - inner).Normalized();
        if (direction == Vector2D.Zero)
            return null;

        // Probe far along the ray and keep the nearest hit beyond the current end.
        Point2D far = end + direction * 1e6;
        double bestDistance = double.MaxValue;
        Point2D? best = null;
        var probe = new LineEntity(end, far);
        foreach (IEntity cutter in cutters)
        {
            if (ReferenceEquals(cutter, target))
                continue;

            foreach (Point2D hit in EntityIntersection.Between(probe, cutter))
            {
                double along = (hit - end).Dot(direction);
                if (along <= Tol)
                    continue;
                if (along < bestDistance)
                {
                    bestDistance = along;
                    best = hit;
                }
            }
        }

        if (best is not { } target_)
            return null;

        var vertices = new List<Point2D>(path.Vertices);
        vertices[atStart ? 0 : ^1] = target_;
        return BuildPath(target, vertices);
    }

    private static IEntity BuildPath(IEntity source, IReadOnlyList<Point2D> points)
    {
        if (source is LineEntity && points.Count == 2)
        {
            var line = (LineEntity)source.Clone();
            line.Start = points[0];
            line.End = points[1];
            return line;
        }

        var polyline = source is PolylineEntity p
            ? (PolylineEntity)p.Clone()
            : CopyStyle(source, new PolylineEntity(points));
        polyline.RestoreState((new List<Point2D>(points), false));
        return polyline;
    }

    // ----- Circle / arc -----

    private static IReadOnlyList<IEntity>? TrimCircle(CircleEntity circle, Point2D pick, IReadOnlyList<Point2D> cutPoints)
    {
        List<double> angles = OnCircleAngles(circle.Center, circle.Radius, cutPoints);
        if (angles.Count < 2)
            return null;

        angles.Sort();
        double clickAngle = AngleOf(circle.Center, pick);

        int count = angles.Count;
        for (int i = 0; i < count; i++)
        {
            double lo = angles[i];
            double hi = i + 1 < count ? angles[i + 1] : angles[0] + Math.PI * 2.0;
            double click = clickAngle < lo ? clickAngle + Math.PI * 2.0 : clickAngle;
            if (click >= lo - Tol && click <= hi + Tol)
            {
                double keptStart = hi % (Math.PI * 2.0);
                double keptSweep = Math.PI * 2.0 - (hi - lo);
                var arc = CopyStyle(circle, new ArcEntity(circle.Center, circle.Radius, keptStart, keptSweep));
                return new IEntity[] { arc };
            }
        }

        return null;
    }

    private static IReadOnlyList<IEntity>? TrimArc(ArcEntity arc, Point2D pick, IReadOnlyList<Point2D> cutPoints)
    {
        double sign = Math.Sign(arc.SweepAngle);
        if (sign == 0)
            return null;

        double span = Math.Abs(arc.SweepAngle);
        var cuts = new List<double>();
        foreach (Point2D point in cutPoints)
        {
            double angle = AngleOf(arc.Center, point);
            if (!GeometryMath.AngleInSweep(arc.StartAngle, arc.SweepAngle, angle))
                continue;

            double position = GeometryMath.NormalizeAngle(sign * (angle - arc.StartAngle));
            if (position <= span + Tol)
                cuts.Add(position);
        }

        cuts.Sort();
        if (cuts.Count == 0)
            return null;

        double clickPos = GeometryMath.NormalizeAngle(sign * (AngleOf(arc.Center, pick) - arc.StartAngle));
        double lower = 0.0;
        double upper = span;
        foreach (double cut in cuts)
        {
            if (cut <= clickPos + Tol)
                lower = cut;
        }

        for (int i = cuts.Count - 1; i >= 0; i--)
        {
            if (cuts[i] >= clickPos - Tol)
                upper = cuts[i];
        }

        var result = new List<IEntity>(2);
        if (lower > Tol)
            result.Add(CopyStyle(arc, new ArcEntity(arc.Center, arc.Radius, arc.StartAngle, sign * lower)));
        if (upper < span - Tol)
            result.Add(CopyStyle(arc, new ArcEntity(arc.Center, arc.Radius, arc.StartAngle + sign * upper, sign * (span - upper))));

        return result.Count == 0 ? null : result;
    }

    private static List<double> OnCircleAngles(Point2D center, double radius, IReadOnlyList<Point2D> points)
    {
        var angles = new List<double>();
        foreach (Point2D point in points)
        {
            if (Math.Abs(center.DistanceTo(point) - radius) <= radius * 1e-3 + Tol)
                angles.Add(GeometryMath.NormalizeAngle(AngleOf(center, point)));
        }

        return angles;
    }

    private static double AngleOf(Point2D center, Point2D point) => Math.Atan2(point.Y - center.Y, point.X - center.X);

    private static TEntity CopyStyle<TEntity>(IEntity source, TEntity target) where TEntity : IEntity
    {
        target.LayerId = source.LayerId;
        target.PartialDrawingId = source.PartialDrawingId;
        target.StrokeOverride = source.StrokeOverride;
        return target;
    }

    /// <summary>A line/polyline flattened to a vertex list with arc-length addressing.</summary>
    private sealed class Path
    {
        private readonly double[] _cumulative; // cumulative length at each vertex

        private Path(IReadOnlyList<Point2D> vertices, bool closed)
        {
            Vertices = vertices;
            Closed = closed;

            int spans = closed ? vertices.Count : vertices.Count - 1;
            _cumulative = new double[vertices.Count + (closed ? 1 : 0)];
            double total = 0.0;
            for (int i = 0; i < spans; i++)
            {
                _cumulative[i] = total;
                total += vertices[i].DistanceTo(vertices[(i + 1) % vertices.Count]);
            }

            _cumulative[spans] = total;
            Length = total;
        }

        public IReadOnlyList<Point2D> Vertices { get; }

        public bool Closed { get; }

        public double Length { get; }

        public static Path From(IEntity entity) => entity switch
        {
            LineEntity line => new Path(new[] { line.Start, line.End }, false),
            PolylineEntity polyline => new Path(polyline.Points, polyline.IsClosed),
            _ => throw new ArgumentException("Not a path entity.", nameof(entity)),
        };

        public double PositionOf(Point2D point, out double distance)
        {
            int spans = Closed ? Vertices.Count : Vertices.Count - 1;
            double bestDistance = double.MaxValue;
            double bestPosition = 0.0;
            for (int i = 0; i < spans; i++)
            {
                Point2D a = Vertices[i];
                Point2D b = Vertices[(i + 1) % Vertices.Count];
                double d = GeometryMath.DistancePointToSegment(point, a, b, out Point2D closest);
                if (d < bestDistance)
                {
                    bestDistance = d;
                    bestPosition = _cumulative[i] + a.DistanceTo(closest);
                }
            }

            distance = bestDistance;
            return bestPosition;
        }

        public Point2D PointAt(double position)
        {
            int spans = Closed ? Vertices.Count : Vertices.Count - 1;
            for (int i = 0; i < spans; i++)
            {
                double start = _cumulative[i];
                double end = _cumulative[i + 1];
                if (position <= end + Tol)
                {
                    Point2D a = Vertices[i];
                    Point2D b = Vertices[(i + 1) % Vertices.Count];
                    double segmentLength = end - start;
                    double t = segmentLength <= Tol ? 0.0 : (position - start) / segmentLength;
                    return a.Lerp(b, GeometryMath.Clamp(t, 0.0, 1.0));
                }
            }

            return Vertices[Closed ? 0 : ^1];
        }

        /// <summary>Vertices of the sub-path between two positions (from ≤ to).</summary>
        public IReadOnlyList<Point2D> Slice(double from, double to)
        {
            var result = new List<Point2D> { PointAt(from) };
            int spans = Closed ? Vertices.Count : Vertices.Count - 1;
            for (int i = 1; i <= spans; i++)
            {
                double position = _cumulative[i];
                if (position > from + Tol && position < to - Tol)
                    result.Add(Vertices[i % Vertices.Count]);
            }

            result.Add(PointAt(to));
            return result;
        }

        /// <summary>Sub-path walking forward from <paramref name="from"/>, wrapping once, to <paramref name="to"/>.</summary>
        public IReadOnlyList<Point2D> SliceForward(double from, double to)
        {
            if (from <= to)
                return Slice(from, to);

            var result = new List<Point2D>(Slice(from, Length));
            IReadOnlyList<Point2D> tail = Slice(0.0, to);
            for (int i = 0; i < tail.Count; i++)
            {
                if (i == 0 && result.Count > 0 && result[^1].DistanceSquaredTo(tail[0]) <= Tol)
                    continue;
                result.Add(tail[i]);
            }

            return result;
        }
    }
}
