using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Model3D;

/// <summary>
/// A triangle mesh in 3D: a vertex list plus triangle indices (three per face). It is the basic
/// 3D geometry carrier of the parallel 3D model space (alongside the 2D Teilbilder); B-Rep can
/// be layered on later.
/// </summary>
public sealed class Mesh3D
{
    private readonly List<Point3D> _vertices;
    private readonly List<int> _indices;

    public Mesh3D(IEnumerable<Point3D> vertices, IEnumerable<int> indices)
    {
        _vertices = new List<Point3D>(vertices);
        _indices = new List<int>(indices);
    }

    public IReadOnlyList<Point3D> Vertices => _vertices;

    /// <summary>Triangle indices (groups of three into <see cref="Vertices"/>).</summary>
    public IReadOnlyList<int> Indices => _indices;

    public BoundingBox3D Bounds => BoundingBox3D.FromPoints(_vertices);

    /// <summary>The unique undirected edges of the triangles (for wireframe display).</summary>
    public IEnumerable<(Point3D A, Point3D B)> Edges()
    {
        var seen = new HashSet<(int, int)>();
        for (int i = 0; i + 2 < _indices.Count; i += 3)
        {
            int a = _indices[i], b = _indices[i + 1], c = _indices[i + 2];
            foreach ((int u, int v) in new[] { (a, b), (b, c), (c, a) })
            {
                var key = u < v ? (u, v) : (v, u);
                if (seen.Add(key))
                    yield return (_vertices[u], _vertices[v]);
            }
        }
    }

    /// <summary>A copy with every vertex transformed by <paramref name="matrix"/>.</summary>
    public Mesh3D Transformed(in Matrix4 matrix)
    {
        Matrix4 m = matrix;
        return new Mesh3D(_vertices.Select(v => m.Transform(v)), _indices);
    }

    /// <summary>An axis-aligned box mesh of the given size centred at <paramref name="center"/>.</summary>
    public static Mesh3D Box(double width, double height, double depth, Point3D center = default)
    {
        double hx = width / 2, hy = height / 2, hz = depth / 2;
        var v = new[]
        {
            new Point3D(center.X - hx, center.Y - hy, center.Z - hz),
            new Point3D(center.X + hx, center.Y - hy, center.Z - hz),
            new Point3D(center.X + hx, center.Y + hy, center.Z - hz),
            new Point3D(center.X - hx, center.Y + hy, center.Z - hz),
            new Point3D(center.X - hx, center.Y - hy, center.Z + hz),
            new Point3D(center.X + hx, center.Y - hy, center.Z + hz),
            new Point3D(center.X + hx, center.Y + hy, center.Z + hz),
            new Point3D(center.X - hx, center.Y + hy, center.Z + hz),
        };
        int[] tris =
        {
            0, 1, 2, 0, 2, 3, // bottom
            4, 6, 5, 4, 7, 6, // top
            0, 4, 5, 0, 5, 1, // front
            1, 5, 6, 1, 6, 2, // right
            2, 6, 7, 2, 7, 3, // back
            3, 7, 4, 3, 4, 0, // left
        };
        return new Mesh3D(v, tris);
    }
}
