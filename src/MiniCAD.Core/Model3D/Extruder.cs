using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Model3D;

/// <summary>Extrudes a closed 2D profile into a 3D prism solid (#90).</summary>
public static class Extruder
{
    /// <summary>
    /// Extrudes <paramref name="profile"/> (a closed polygon in XY) from <paramref name="baseZ"/>
    /// upward by <paramref name="height"/>, producing side walls plus a bottom and top cap.
    /// </summary>
    public static Mesh3D Extrude(IReadOnlyList<Point2D> profile, double baseZ, double height)
    {
        int n = profile.Count;
        if (n < 3 || Math.Abs(height) <= GeometryMath.Epsilon)
            return new Mesh3D(Array.Empty<Point3D>(), Array.Empty<int>());

        double topZ = baseZ + height;
        var verts = new List<Point3D>(2 * n);
        foreach (Point2D p in profile) verts.Add(new Point3D(p.X, p.Y, baseZ));   // bottom: 0..n-1
        foreach (Point2D p in profile) verts.Add(new Point3D(p.X, p.Y, topZ));    // top:    n..2n-1

        var tris = new List<int>();
        for (int i = 0; i < n; i++)
        {
            int b0 = i, b1 = (i + 1) % n, t0 = n + i, t1 = n + (i + 1) % n;
            tris.AddRange(new[] { b0, b1, t1, b0, t1, t0 }); // side quad
        }

        List<int> cap = PolygonTriangulator.Triangulate(profile);
        for (int i = 0; i + 2 < cap.Count; i += 3)
        {
            int a = cap[i], b = cap[i + 1], c = cap[i + 2];
            tris.AddRange(new[] { a, c, b });                 // bottom cap (faces -Z)
            tris.AddRange(new[] { n + a, n + b, n + c });     // top cap (faces +Z)
        }

        return new Mesh3D(verts, tris);
    }
}
