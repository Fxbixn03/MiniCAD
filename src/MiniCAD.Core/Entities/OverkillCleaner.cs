using System.Linq;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Styling;

namespace MiniCAD.Core.Entities;

/// <summary>Options for <see cref="OverkillCleaner"/> (#189).</summary>
public sealed class OverkillOptions
{
    /// <summary>Geometric tolerance for treating points/segments as coincident.</summary>
    public double Tolerance { get; set; } = 1e-6;

    /// <summary>When false (default) only entities on the same layer/style/construction are merged.</summary>
    public bool IgnoreProperties { get; set; }
}

/// <summary>The outcome of an overkill pass: which entities to delete and which merged ones to add.</summary>
public sealed class OverkillResult
{
    public OverkillResult(IReadOnlyList<IEntity> removed, IReadOnlyList<IEntity> added)
    {
        Removed = removed;
        Added = added;
    }

    public IReadOnlyList<IEntity> Removed { get; }
    public IReadOnlyList<IEntity> Added { get; }

    /// <summary>Net number of entities the operation removes from the drawing.</summary>
    public int NetRemoved => Removed.Count - Added.Count;
}

/// <summary>
/// Removes redundant geometry (#189, "Overkill"): exact duplicates are reduced to one, and
/// collinear lines that overlap or touch are fused into a single line. By default only entities
/// sharing the same layer/style/construction are combined; set
/// <see cref="OverkillOptions.IgnoreProperties"/> to merge regardless. The result is reported as
/// entities to remove plus merged entities to add, so the caller can apply it as one undoable step.
/// </summary>
public static class OverkillCleaner
{
    public static OverkillResult Clean(IEnumerable<IEntity> entities, OverkillOptions? options = null)
    {
        options ??= new OverkillOptions();
        double tol = options.Tolerance;

        var removed = new List<IEntity>();
        var added = new List<IEntity>();

        // Partition by appearance so we never merge across layers/styles unless asked to.
        IEnumerable<IGrouping<object, IEntity>> groups = options.IgnoreProperties
            ? new[] { (IGrouping<object, IEntity>)new SingleGroup(entities) }
            : entities.GroupBy(PropertyKey);

        foreach (IGrouping<object, IEntity> group in groups)
        {
            var lines = group.OfType<LineEntity>().ToList();
            MergeLines(lines, tol, removed, added);

            // Exact duplicates among the remaining (non-line) types.
            var others = group.Where(e => e is not LineEntity).ToList();
            RemoveDuplicates(others, tol, removed);
        }

        return new OverkillResult(removed, added);
    }

    private static object PropertyKey(IEntity entity)
        => (entity.LayerId, entity.StrokeOverride, entity.IsConstruction);

    // ----- Collinear line fusion -----

    private static void MergeLines(List<LineEntity> lines, double tol, List<IEntity> removed, List<IEntity> added)
    {
        var clusters = new List<List<LineEntity>>();
        foreach (LineEntity line in lines)
        {
            List<LineEntity>? cluster = clusters.FirstOrDefault(c => Collinear(c[0], line, tol));
            if (cluster is null)
                clusters.Add(new List<LineEntity> { line });
            else
                cluster.Add(line);
        }

        foreach (List<LineEntity> cluster in clusters)
        {
            LineEntity reference = cluster[0];
            Vector2D dir = (reference.End - reference.Start).Normalized();
            Point2D origin = reference.Start;

            var intervals = cluster
                .Select(l => Order(Project(l.Start, origin, dir), Project(l.End, origin, dir)))
                .OrderBy(iv => iv.Min)
                .ToList();

            var mergedIntervals = new List<(double Min, double Max)>();
            (double Min, double Max) current = intervals[0];
            for (int i = 1; i < intervals.Count; i++)
            {
                if (intervals[i].Min <= current.Max + tol)
                    current = (current.Min, Math.Max(current.Max, intervals[i].Max));
                else
                {
                    mergedIntervals.Add(current);
                    current = intervals[i];
                }
            }
            mergedIntervals.Add(current);

            if (mergedIntervals.Count >= cluster.Count)
                continue; // nothing actually fused in this cluster

            foreach (LineEntity l in cluster)
                removed.Add(l);
            foreach ((double min, double max) in mergedIntervals)
            {
                var line = new LineEntity(origin + dir * min, origin + dir * max);
                CopyProperties(line, reference);
                added.Add(line);
            }
        }
    }

    private static bool Collinear(LineEntity a, LineEntity b, double tol)
    {
        Vector2D da = a.End - a.Start;
        Vector2D db = b.End - b.Start;
        double lenA = da.Length, lenB = db.Length;
        if (lenA <= tol || lenB <= tol)
            return false;

        Vector2D na = da / lenA;
        // Parallel: the direction cross product is ~0.
        if (Math.Abs(na.X * (db.Y / lenB) - na.Y * (db.X / lenB)) > tol)
            return false;

        // Same infinite line: b's endpoints have ~0 perpendicular distance to a's line.
        return PerpDistance(b.Start, a.Start, na) <= tol && PerpDistance(b.End, a.Start, na) <= tol;
    }

    private static double PerpDistance(Point2D p, Point2D origin, Vector2D dir)
    {
        Vector2D v = p - origin;
        double along = v.X * dir.X + v.Y * dir.Y;
        Vector2D proj = dir * along;
        return new Vector2D(v.X - proj.X, v.Y - proj.Y).Length;
    }

    private static double Project(Point2D p, Point2D origin, Vector2D dir)
        => (p.X - origin.X) * dir.X + (p.Y - origin.Y) * dir.Y;

    private static (double Min, double Max) Order(double a, double b) => a <= b ? (a, b) : (b, a);

    // ----- Exact duplicate removal -----

    private static void RemoveDuplicates(List<IEntity> entities, double tol, List<IEntity> removed)
    {
        var kept = new List<IEntity>();
        foreach (IEntity entity in entities)
        {
            if (kept.Any(k => SameGeometry(k, entity, tol)))
                removed.Add(entity);
            else
                kept.Add(entity);
        }
    }

    private static bool SameGeometry(IEntity a, IEntity b, double tol)
    {
        return (a, b) switch
        {
            (CircleEntity ca, CircleEntity cb) =>
                ca.Center.DistanceTo(cb.Center) <= tol && Math.Abs(ca.Radius - cb.Radius) <= tol,
            (PointEntity pa, PointEntity pb) => pa.Position.DistanceTo(pb.Position) <= tol,
            (ArcEntity aa, ArcEntity ab) =>
                aa.Center.DistanceTo(ab.Center) <= tol && Math.Abs(aa.Radius - ab.Radius) <= tol
                && AngleClose(aa.StartAngle, ab.StartAngle, tol) && Math.Abs(aa.SweepAngle - ab.SweepAngle) <= tol,
            (PolylineEntity pa, PolylineEntity pb) => SamePolyline(pa, pb, tol),
            _ => false,
        };
    }

    private static bool SamePolyline(PolylineEntity a, PolylineEntity b, double tol)
    {
        if (a.IsClosed != b.IsClosed || a.Points.Count != b.Points.Count)
            return false;
        bool forward = true, reverse = true;
        int n = a.Points.Count;
        for (int i = 0; i < n; i++)
        {
            if (a.Points[i].DistanceTo(b.Points[i]) > tol)
                forward = false;
            if (a.Points[i].DistanceTo(b.Points[n - 1 - i]) > tol)
                reverse = false;
        }
        return forward || reverse;
    }

    private static bool AngleClose(double a, double b, double tol)
        => Math.Abs(GeometryMath.NormalizeAngle(a - b)) <= tol
           || Math.Abs(GeometryMath.NormalizeAngle(a - b) - Math.PI * 2) <= tol;

    private static void CopyProperties(IEntity target, IEntity source)
    {
        target.LayerId = source.LayerId;
        target.PartialDrawingId = source.PartialDrawingId;
        target.StrokeOverride = source.StrokeOverride;
        target.IsConstruction = source.IsConstruction;
    }

    /// <summary>A single all-in-one grouping used when properties are ignored.</summary>
    private sealed class SingleGroup : List<IEntity>, IGrouping<object, IEntity>
    {
        public SingleGroup(IEnumerable<IEntity> items) : base(items) { }
        public object Key => string.Empty;
    }
}
