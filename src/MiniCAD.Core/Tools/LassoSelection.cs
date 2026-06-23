using MiniCAD.Core.Entities;
using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Tools;

/// <summary>
/// Freehand (lasso) selection (#228): selects the entities whose characteristic points fall inside
/// a hand-drawn polygon. Like the rubber-band box, a window lasso (drawn left→right) keeps only
/// fully-enclosed objects, while a crossing lasso (right→left) also takes touched ones. Entities
/// are sampled by their snap points (vertices/centers), falling back to their bounds corners.
/// </summary>
public static class LassoSelection
{
    public static List<IEntity> Select(IReadOnlyList<Point2D> polygon, IEnumerable<IEntity> entities, bool crossing)
    {
        ArgumentNullException.ThrowIfNull(polygon);
        ArgumentNullException.ThrowIfNull(entities);

        var result = new List<IEntity>();
        if (polygon.Count < 3)
            return result;

        foreach (IEntity entity in entities)
        {
            List<Point2D> samples = SamplePoints(entity);
            if (samples.Count == 0)
                continue;

            bool anyInside = false;
            bool allInside = true;
            foreach (Point2D p in samples)
            {
                if (GeometryMath.PointInPolygon(polygon, p))
                    anyInside = true;
                else
                    allInside = false;
            }

            if (crossing ? anyInside : allInside)
                result.Add(entity);
        }

        return result;
    }

    private static List<Point2D> SamplePoints(IEntity entity)
    {
        var points = entity.SnapPoints.ToList();
        if (points.Count > 0)
            return points;

        Rect2D b = entity.Bounds;
        return new List<Point2D>
        {
            new(b.MinX, b.MinY),
            new(b.MaxX, b.MinY),
            new(b.MaxX, b.MaxY),
            new(b.MinX, b.MaxY),
        };
    }
}
