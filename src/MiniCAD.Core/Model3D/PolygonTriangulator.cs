using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Model3D;

/// <summary>Ear-clipping triangulation of a simple 2D polygon (used for extrusion/revolve caps).</summary>
public static class PolygonTriangulator
{
    /// <summary>Returns triangle index triples into <paramref name="polygon"/> (CCW-oriented output).</summary>
    public static List<int> Triangulate(IReadOnlyList<Point2D> polygon)
    {
        var triangles = new List<int>();
        int n = polygon.Count;
        if (n < 3)
            return triangles;

        // Work on a mutable index ring, oriented CCW so "ear" convexity tests are consistent.
        var indices = new List<int>(n);
        if (SignedArea(polygon) < 0)
            for (int i = n - 1; i >= 0; i--) indices.Add(i);
        else
            for (int i = 0; i < n; i++) indices.Add(i);

        int guard = 0;
        while (indices.Count > 3 && guard++ < n * n)
        {
            bool clipped = false;
            for (int i = 0; i < indices.Count; i++)
            {
                int i0 = indices[(i - 1 + indices.Count) % indices.Count];
                int i1 = indices[i];
                int i2 = indices[(i + 1) % indices.Count];
                Point2D a = polygon[i0], b = polygon[i1], c = polygon[i2];

                if (Cross(a, b, c) <= 0)
                    continue; // reflex vertex, not an ear

                if (AnyPointInside(polygon, indices, i0, i1, i2, a, b, c))
                    continue;

                triangles.Add(i0);
                triangles.Add(i1);
                triangles.Add(i2);
                indices.RemoveAt(i);
                clipped = true;
                break;
            }

            if (!clipped)
                break; // degenerate polygon; stop with what we have
        }

        if (indices.Count == 3)
        {
            triangles.Add(indices[0]);
            triangles.Add(indices[1]);
            triangles.Add(indices[2]);
        }

        return triangles;
    }

    private static double SignedArea(IReadOnlyList<Point2D> p)
    {
        double area = 0;
        for (int i = 0; i < p.Count; i++)
        {
            Point2D a = p[i], b = p[(i + 1) % p.Count];
            area += a.X * b.Y - b.X * a.Y;
        }
        return area * 0.5;
    }

    private static double Cross(Point2D a, Point2D b, Point2D c)
        => (b.X - a.X) * (c.Y - a.Y) - (b.Y - a.Y) * (c.X - a.X);

    private static bool AnyPointInside(IReadOnlyList<Point2D> polygon, List<int> indices, int i0, int i1, int i2,
        Point2D a, Point2D b, Point2D c)
    {
        foreach (int idx in indices)
        {
            if (idx == i0 || idx == i1 || idx == i2)
                continue;
            if (PointInTriangle(polygon[idx], a, b, c))
                return true;
        }
        return false;
    }

    private static bool PointInTriangle(Point2D p, Point2D a, Point2D b, Point2D c)
    {
        double d1 = Cross(a, b, p);
        double d2 = Cross(b, c, p);
        double d3 = Cross(c, a, p);
        bool hasNeg = d1 < 0 || d2 < 0 || d3 < 0;
        bool hasPos = d1 > 0 || d2 > 0 || d3 > 0;
        return !(hasNeg && hasPos);
    }
}
