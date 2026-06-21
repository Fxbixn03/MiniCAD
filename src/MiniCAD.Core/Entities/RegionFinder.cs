using System.Linq;
using MiniCAD.Core.Geometry;
using MiniCAD.Core.Model3D;

namespace MiniCAD.Core.Entities;

/// <summary>
/// Finds the closed region a pick point lies in (#117): the tightest closed contour enclosing
/// the point, with any fully-contained closed contours reported as islands (holes). It works on
/// the document's closed entities (closed polylines, circles, full ellipses) — the common case
/// for extrusion profiles and hatch boundaries.
/// </summary>
public static class RegionFinder
{
    public static Region2D? FindRegion(IEnumerable<IEntity> entities, Point2D pick)
    {
        var contours = entities
            .Select(ProfileExtractor.FromEntity)
            .Where(c => c is { Count: >= 3 })
            .Select(c => c!)
            .ToList();

        // The enclosing region's outer boundary is the smallest-area contour containing the pick.
        IReadOnlyList<Point2D>? outer = contours
            .Where(c => GeometryMath.PointInPolygon(c, pick))
            .OrderBy(Area)
            .FirstOrDefault();

        if (outer is null)
            return null;

        double outerArea = Area(outer);
        var holes = contours
            .Where(c => !ReferenceEquals(c, outer)
                     && Area(c) < outerArea
                     && !GeometryMath.PointInPolygon(c, pick)
                     && c.All(p => GeometryMath.PointInPolygon(outer, p)))
            .ToList();

        return new Region2D(outer, holes);
    }

    private static double Area(IReadOnlyList<Point2D> polygon)
    {
        double area = 0;
        for (int i = 0; i < polygon.Count; i++)
        {
            Point2D a = polygon[i], b = polygon[(i + 1) % polygon.Count];
            area += a.X * b.Y - b.X * a.Y;
        }
        return Math.Abs(area) * 0.5;
    }
}
