using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Model3D;

/// <summary>Builds a solid of revolution by rotating a closed 2D profile around the Y axis (#127).</summary>
public static class Revolver
{
    /// <summary>
    /// Revolves <paramref name="profile"/> (XY, X read as radius) a full turn around the Y axis
    /// in <paramref name="segments"/> steps. A closed profile yields a closed surface (no caps).
    /// </summary>
    public static Mesh3D Revolve(IReadOnlyList<Point2D> profile, int segments = 32)
    {
        int n = profile.Count;
        if (n < 3 || segments < 3)
            return new Mesh3D(Array.Empty<Point3D>(), Array.Empty<int>());

        var verts = new List<Point3D>(n * segments);
        for (int j = 0; j < segments; j++)
        {
            double a = 2 * Math.PI * j / segments;
            double cos = Math.Cos(a), sin = Math.Sin(a);
            foreach (Point2D p in profile)
            {
                double r = Math.Abs(p.X);
                verts.Add(new Point3D(r * cos, p.Y, r * sin));
            }
        }

        var tris = new List<int>();
        for (int j = 0; j < segments; j++)
        {
            int jn = j * n;
            int j1 = (j + 1) % segments * n;
            for (int i = 0; i < n; i++)
            {
                int i1 = (i + 1) % n;
                tris.AddRange(new[] { jn + i, j1 + i, j1 + i1, jn + i, j1 + i1, jn + i1 });
            }
        }

        return new Mesh3D(verts, tris);
    }
}
