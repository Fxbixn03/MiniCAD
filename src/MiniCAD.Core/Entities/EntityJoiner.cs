using System.Linq;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Entities;

/// <summary>
/// Joins lines and open polylines that meet at shared endpoints into connected polylines (#186).
/// Chains whose ends touch (within tolerance) are stitched together — reversing segments as needed
/// — collinear runs are collapsed into single segments, and a chain that closes back on itself
/// becomes a closed polyline. Each connected run yields one result entity; the caller replaces the
/// consumed originals via undoable commands.
/// </summary>
public static class EntityJoiner
{
    /// <summary>The subset of <paramref name="entities"/> that can take part in a join.</summary>
    public static IReadOnlyList<IEntity> CollectJoinable(IEnumerable<IEntity> entities)
        => entities.Where(IsJoinable).ToList();

    private static bool IsJoinable(IEntity entity) => entity switch
    {
        LineEntity => true,
        PolylineEntity { IsClosed: false } p => p.Points.Count >= 2,
        _ => false,
    };

    /// <summary>
    /// Returns one entity per connected run. When nothing connects, the result has the same count
    /// as the joinable input (each segment maps to itself), which the caller can use to detect a
    /// no-op.
    /// </summary>
    public static IReadOnlyList<IEntity> Join(IEnumerable<IEntity> entities, double tolerance)
    {
        var joinable = CollectJoinable(entities);
        IEntity? style = joinable.FirstOrDefault();

        var chains = joinable.Select(ToPoints).ToList();

        bool merged = true;
        while (merged)
        {
            merged = false;
            for (int i = 0; i < chains.Count && !merged; i++)
            for (int j = i + 1; j < chains.Count && !merged; j++)
            {
                if (TryStitch(chains[i], chains[j], tolerance, out List<Point2D> combined))
                {
                    chains[i] = combined;
                    chains.RemoveAt(j);
                    merged = true;
                }
            }
        }

        var result = new List<IEntity>();
        foreach (List<Point2D> chain in chains)
        {
            bool closed = chain.Count >= 4 && chain[0].DistanceTo(chain[^1]) <= tolerance;
            if (closed)
                chain.RemoveAt(chain.Count - 1);

            List<Point2D> simplified = CollapseCollinear(chain, closed, tolerance);

            IEntity entity = !closed && simplified.Count == 2
                ? new LineEntity(simplified[0], simplified[1])
                : new PolylineEntity(simplified, closed);

            if (style is { })
            {
                entity.LayerId = style.LayerId;
                entity.PartialDrawingId = style.PartialDrawingId;
                entity.StrokeOverride = style.StrokeOverride;
                entity.IsConstruction = style.IsConstruction;
            }
            result.Add(entity);
        }

        return result;
    }

    private static List<Point2D> ToPoints(IEntity entity) => entity switch
    {
        LineEntity line => new List<Point2D> { line.Start, line.End },
        PolylineEntity poly => new List<Point2D>(poly.Points),
        _ => new List<Point2D>(),
    };

    private static bool TryStitch(List<Point2D> a, List<Point2D> b, double tol, out List<Point2D> combined)
    {
        combined = a;
        if (a.Count == 0 || b.Count == 0)
            return false;

        if (a[^1].DistanceTo(b[0]) <= tol)
            combined = Concat(a, b, reverseB: false);
        else if (a[^1].DistanceTo(b[^1]) <= tol)
            combined = Concat(a, b, reverseB: true);
        else if (a[0].DistanceTo(b[^1]) <= tol)
            combined = Concat(b, a, reverseB: false);
        else if (a[0].DistanceTo(b[0]) <= tol)
            combined = Concat(ReversedCopy(a), b, reverseB: false);
        else
            return false;

        return true;
    }

    private static List<Point2D> Concat(List<Point2D> head, List<Point2D> tail, bool reverseB)
    {
        var result = new List<Point2D>(head);
        IEnumerable<Point2D> rest = reverseB ? Enumerable.Reverse(tail) : tail;
        result.AddRange(rest.Skip(1)); // drop the shared joint point
        return result;
    }

    private static List<Point2D> ReversedCopy(List<Point2D> points)
    {
        var copy = new List<Point2D>(points);
        copy.Reverse();
        return copy;
    }

    /// <summary>Removes interior vertices that lie on the line through their neighbours.</summary>
    private static List<Point2D> CollapseCollinear(List<Point2D> points, bool closed, double tol)
    {
        if (points.Count < 3)
            return new List<Point2D>(points);

        var kept = new List<Point2D>(points);
        int i = 1;
        while (kept.Count > 2 && i < kept.Count - 1)
        {
            if (GeometryMath.DistancePointToSegment(kept[i], kept[i - 1], kept[i + 1], out _) <= tol)
                kept.RemoveAt(i);
            else
                i++;
        }

        // For a closed loop, also test the wrap-around vertex at index 0.
        if (closed && kept.Count > 3 &&
            GeometryMath.DistancePointToSegment(kept[0], kept[^1], kept[1], out _) <= tol)
            kept.RemoveAt(0);

        return kept;
    }
}
