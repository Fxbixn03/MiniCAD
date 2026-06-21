using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Model3D;

/// <summary>Sweeps a closed 2D profile along a 3D path (translational sweep) into a solid (#127).</summary>
public static class Sweeper
{
    /// <summary>
    /// Sweeps <paramref name="profile"/> (XY) along <paramref name="path"/>: the profile is
    /// translated to each path point (orientation kept fixed), consecutive sections joined by
    /// side walls, with caps at the two ends.
    /// </summary>
    public static Mesh3D Sweep(IReadOnlyList<Point2D> profile, IReadOnlyList<Point3D> path)
    {
        int n = profile.Count;
        int m = path.Count;
        if (n < 3 || m < 2)
            return new Mesh3D(Array.Empty<Point3D>(), Array.Empty<int>());

        var verts = new List<Point3D>(n * m);
        foreach (Point3D c in path)
            foreach (Point2D p in profile)
                verts.Add(new Point3D(p.X + c.X, p.Y + c.Y, c.Z));

        var tris = new List<int>();
        for (int k = 0; k < m - 1; k++)
        {
            int kn = k * n, k1 = (k + 1) * n;
            for (int i = 0; i < n; i++)
            {
                int i1 = (i + 1) % n;
                tris.AddRange(new[] { kn + i, k1 + i, k1 + i1, kn + i, k1 + i1, kn + i1 });
            }
        }

        List<int> cap = PolygonTriangulator.Triangulate(profile);
        int last = (m - 1) * n;
        for (int i = 0; i + 2 < cap.Count; i += 3)
        {
            int a = cap[i], b = cap[i + 1], c = cap[i + 2];
            tris.AddRange(new[] { a, c, b });                          // start cap
            tris.AddRange(new[] { last + a, last + b, last + c });     // end cap
        }

        return new Mesh3D(verts, tris);
    }
}
