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

    private struct EdgeAccumulator
    {
        public int Count;
        public Vector3D Normal;
        public bool HasNormal;
        public bool Feature;
        public int A;
        public int B;
    }

    /// <summary>
    /// The "feature" edges only — model boundaries plus crease edges where adjacent faces meet at
    /// an angle steeper than <paramref name="thresholdDegrees"/>. Edges between coplanar triangles
    /// (a quad face's triangulation diagonal, a flat region split by CSG, smooth facets) are
    /// dropped, giving a clean outline instead of a mesh of triangle edges. Vertices are welded by
    /// position first, so it also works on CSG results that don't share indices.
    /// </summary>
    public IEnumerable<(Point3D A, Point3D B)> FeatureEdges(double thresholdDegrees = 25.0)
    {
        var result = new List<(Point3D, Point3D)>();
        if (_indices.Count < 3)
            return result;

        double diagonal = Bounds.Size.Length;
        double tol = Math.Max(diagonal * 1e-5, 1e-6);
        double cosThreshold = Math.Cos(thresholdDegrees * Math.PI / 180.0);

        var weldIds = new Dictionary<(long, long, long), int>();
        var rep = new List<Point3D>();

        int Weld(Point3D p)
        {
            var key = ((long)Math.Round(p.X / tol), (long)Math.Round(p.Y / tol), (long)Math.Round(p.Z / tol));
            if (!weldIds.TryGetValue(key, out int id))
            {
                id = rep.Count;
                weldIds[key] = id;
                rep.Add(p);
            }
            return id;
        }

        var edges = new Dictionary<(int, int), EdgeAccumulator>();
        for (int i = 0; i + 2 < _indices.Count; i += 3)
        {
            Point3D pa = _vertices[_indices[i]], pb = _vertices[_indices[i + 1]], pc = _vertices[_indices[i + 2]];
            int a = Weld(pa), b = Weld(pb), c = Weld(pc);

            Vector3D n = (pb - pa).Cross(pc - pa);
            double len = n.Length;
            bool hasNormal = len > 1e-12;
            Vector3D normal = hasNormal ? n * (1.0 / len) : default;

            Accumulate(edges, a, b, normal, hasNormal, cosThreshold);
            Accumulate(edges, b, c, normal, hasNormal, cosThreshold);
            Accumulate(edges, c, a, normal, hasNormal, cosThreshold);
        }

        foreach (EdgeAccumulator e in edges.Values)
            if (e.Count == 1 || e.Feature) // boundary edge or crease above the threshold
                result.Add((rep[e.A], rep[e.B]));

        return result;
    }

    private static void Accumulate(Dictionary<(int, int), EdgeAccumulator> edges,
        int u, int v, Vector3D normal, bool hasNormal, double cosThreshold)
    {
        (int, int) key = u < v ? (u, v) : (v, u);
        if (!edges.TryGetValue(key, out EdgeAccumulator acc))
        {
            edges[key] = new EdgeAccumulator
            {
                Count = 1, Normal = normal, HasNormal = hasNormal, Feature = false, A = key.Item1, B = key.Item2,
            };
            return;
        }

        acc.Count++;
        if (acc.HasNormal && hasNormal)
        {
            if (acc.Normal.Dot(normal) < cosThreshold)
                acc.Feature = true; // dihedral angle exceeds the threshold → a crease
        }
        else
        {
            acc.Feature = true; // a degenerate neighbour — keep the edge to be safe
        }
        edges[key] = acc;
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

    /// <summary>A cylinder of the given radius/height about the Z axis, centred at <paramref name="center"/>.</summary>
    public static Mesh3D Cylinder(double radius, double height, int segments = 24, Point3D center = default)
    {
        segments = Math.Max(3, segments);
        double hz = height / 2;
        var verts = new List<Point3D>();
        var tris = new List<int>();

        for (int i = 0; i < segments; i++)
        {
            double a = 2 * Math.PI * i / segments;
            double x = center.X + radius * Math.Cos(a);
            double y = center.Y + radius * Math.Sin(a);
            verts.Add(new Point3D(x, y, center.Z - hz)); // bottom ring: even
            verts.Add(new Point3D(x, y, center.Z + hz)); // top ring: odd
        }

        int cb = verts.Count; verts.Add(new Point3D(center.X, center.Y, center.Z - hz));
        int ct = verts.Count; verts.Add(new Point3D(center.X, center.Y, center.Z + hz));

        for (int i = 0; i < segments; i++)
        {
            int b0 = i * 2, t0 = i * 2 + 1;
            int b1 = (i + 1) % segments * 2, t1 = b1 + 1;
            tris.AddRange(new[] { b0, b1, t1, b0, t1, t0 }); // side
            tris.AddRange(new[] { cb, b1, b0 });             // bottom cap
            tris.AddRange(new[] { ct, t0, t1 });             // top cap
        }

        return new Mesh3D(verts, tris);
    }

    /// <summary>A cone (apex up the Z axis) of the given base radius/height, centred at <paramref name="center"/>.</summary>
    public static Mesh3D Cone(double radius, double height, int segments = 24, Point3D center = default)
    {
        segments = Math.Max(3, segments);
        double hz = height / 2;
        var verts = new List<Point3D>();
        var tris = new List<int>();

        for (int i = 0; i < segments; i++)
        {
            double a = 2 * Math.PI * i / segments;
            verts.Add(new Point3D(center.X + radius * Math.Cos(a), center.Y + radius * Math.Sin(a), center.Z - hz));
        }

        int apex = verts.Count; verts.Add(new Point3D(center.X, center.Y, center.Z + hz));
        int baseCenter = verts.Count; verts.Add(new Point3D(center.X, center.Y, center.Z - hz));

        for (int i = 0; i < segments; i++)
        {
            int b0 = i, b1 = (i + 1) % segments;
            tris.AddRange(new[] { b0, b1, apex });       // side
            tris.AddRange(new[] { baseCenter, b1, b0 }); // base cap
        }

        return new Mesh3D(verts, tris);
    }

    /// <summary>A UV sphere of the given radius, centred at <paramref name="center"/>.</summary>
    public static Mesh3D Sphere(double radius, int segments = 24, int rings = 16, Point3D center = default)
    {
        segments = Math.Max(3, segments);
        rings = Math.Max(2, rings);
        var verts = new List<Point3D>();
        var tris = new List<int>();

        for (int r = 0; r <= rings; r++)
        {
            double phi = Math.PI * r / rings;          // 0..π (pole to pole)
            double z = Math.Cos(phi), rad = Math.Sin(phi);
            for (int s = 0; s <= segments; s++)
            {
                double theta = 2 * Math.PI * s / segments;
                verts.Add(new Point3D(
                    center.X + radius * rad * Math.Cos(theta),
                    center.Y + radius * rad * Math.Sin(theta),
                    center.Z + radius * z));
            }
        }

        int stride = segments + 1;
        for (int r = 0; r < rings; r++)
        {
            for (int s = 0; s < segments; s++)
            {
                int a = r * stride + s;
                int b = a + stride;
                tris.AddRange(new[] { a, b, a + 1, a + 1, b, b + 1 });
            }
        }

        return new Mesh3D(verts, tris);
    }
}
