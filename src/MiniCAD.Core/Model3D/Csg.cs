using MiniCAD.Core.Geometry;

namespace MiniCAD.Core.Model3D;

/// <summary>
/// Constructive solid geometry (CSG) on triangle meshes via BSP trees — union, difference and
/// intersection (#126). Port of the well-known BSP-CSG algorithm; input meshes are treated as
/// closed solids and the result is re-triangulated.
/// </summary>
public static class Csg
{
    private const double Epsilon = 1e-5;

    public static Mesh3D Union(Mesh3D a, Mesh3D b) => Operate(a, b, Op.Union);
    public static Mesh3D Subtract(Mesh3D a, Mesh3D b) => Operate(a, b, Op.Subtract);
    public static Mesh3D Intersect(Mesh3D a, Mesh3D b) => Operate(a, b, Op.Intersect);

    private enum Op { Union, Subtract, Intersect }

    private static Mesh3D Operate(Mesh3D meshA, Mesh3D meshB, Op op)
    {
        var a = new Node(ToPolygons(meshA));
        var b = new Node(ToPolygons(meshB));

        switch (op)
        {
            case Op.Union:
                a.ClipTo(b); b.ClipTo(a); b.Invert(); b.ClipTo(a); b.Invert(); a.Build(b.AllPolygons());
                break;
            case Op.Subtract:
                a.Invert(); a.ClipTo(b); b.ClipTo(a); b.Invert(); b.ClipTo(a); b.Invert(); a.Build(b.AllPolygons()); a.Invert();
                break;
            case Op.Intersect:
                a.Invert(); b.ClipTo(a); b.Invert(); a.ClipTo(b); b.ClipTo(a); a.Build(b.AllPolygons()); a.Invert();
                break;
        }

        return ToMesh(a.AllPolygons());
    }

    // ----- Conversion -----

    private static List<Polygon> ToPolygons(Mesh3D mesh)
    {
        IReadOnlyList<Point3D> v = mesh.Vertices;
        IReadOnlyList<int> idx = mesh.Indices;

        // CSG requires outward-facing normals. The app's primitive meshes use inward winding, so
        // orient via the signed volume (divergence theorem): a negative volume ⇒ inward, flip it.
        bool flip = SignedVolume(v, idx) < 0;

        var polys = new List<Polygon>();
        for (int i = 0; i + 2 < idx.Count; i += 3)
        {
            Vector3D a = Vec(v[idx[i]]), b = Vec(v[idx[i + 1]]), c = Vec(v[idx[i + 2]]);
            var tri = flip
                ? new List<Vector3D> { c, b, a }
                : new List<Vector3D> { a, b, c };
            var plane = Plane.FromPoints(tri[0], tri[1], tri[2]);
            if (plane.HasValue)
                polys.Add(new Polygon(tri, plane.Value));
        }
        return polys;
    }

    private static double SignedVolume(IReadOnlyList<Point3D> v, IReadOnlyList<int> idx)
    {
        double vol = 0;
        for (int i = 0; i + 2 < idx.Count; i += 3)
        {
            Vector3D a = Vec(v[idx[i]]), b = Vec(v[idx[i + 1]]), c = Vec(v[idx[i + 2]]);
            vol += a.Dot(b.Cross(c));
        }
        return vol / 6.0;
    }

    private static Mesh3D ToMesh(List<Polygon> polygons)
    {
        var verts = new List<Point3D>();
        var tris = new List<int>();
        foreach (Polygon poly in polygons)
        {
            int baseIndex = verts.Count;
            foreach (Vector3D vtx in poly.Vertices)
                verts.Add(new Point3D(vtx.X, vtx.Y, vtx.Z));
            for (int i = 2; i < poly.Vertices.Count; i++) // fan triangulation (CSG polygons are convex)
                tris.AddRange(new[] { baseIndex, baseIndex + i - 1, baseIndex + i });
        }
        return new Mesh3D(verts, tris);
    }

    private static Vector3D Vec(Point3D p) => new(p.X, p.Y, p.Z);

    // ----- BSP primitives -----

    private readonly struct Plane
    {
        public readonly Vector3D Normal;
        public readonly double W;

        private Plane(Vector3D normal, double w) { Normal = normal; W = w; }

        public static Plane? FromPoints(Vector3D a, Vector3D b, Vector3D c)
        {
            Vector3D n = (b - a).Cross(c - a);
            if (n.Length <= Epsilon)
                return null;
            n = n.Normalized();
            return new Plane(n, n.Dot(a));
        }

        public Plane Flipped() => new(-Normal, -W);

        private const int Coplanar = 0, Front = 1, Back = 2, Spanning = 3;

        public void Split(Polygon polygon, List<Polygon> coplanarFront, List<Polygon> coplanarBack,
            List<Polygon> front, List<Polygon> back)
        {
            int polygonType = 0;
            var types = new int[polygon.Vertices.Count];
            for (int i = 0; i < polygon.Vertices.Count; i++)
            {
                double t = Normal.Dot(polygon.Vertices[i]) - W;
                int type = t < -Epsilon ? Back : t > Epsilon ? Front : Coplanar;
                polygonType |= type;
                types[i] = type;
            }

            switch (polygonType)
            {
                case Coplanar:
                    (Normal.Dot(polygon.Plane.Normal) > 0 ? coplanarFront : coplanarBack).Add(polygon);
                    break;
                case Front:
                    front.Add(polygon);
                    break;
                case Back:
                    back.Add(polygon);
                    break;
                default: // Spanning
                    var f = new List<Vector3D>();
                    var b = new List<Vector3D>();
                    for (int i = 0; i < polygon.Vertices.Count; i++)
                    {
                        int j = (i + 1) % polygon.Vertices.Count;
                        int ti = types[i], tj = types[j];
                        Vector3D vi = polygon.Vertices[i], vj = polygon.Vertices[j];
                        if (ti != Back) f.Add(vi);
                        if (ti != Front) b.Add(vi);
                        if ((ti | tj) == Spanning)
                        {
                            double t = (W - Normal.Dot(vi)) / Normal.Dot(vj - vi);
                            Vector3D mid = vi + (vj - vi) * t;
                            f.Add(mid);
                            b.Add(mid);
                        }
                    }
                    if (f.Count >= 3) front.Add(new Polygon(f, polygon.Plane));
                    if (b.Count >= 3) back.Add(new Polygon(b, polygon.Plane));
                    break;
            }
        }
    }

    private sealed class Polygon
    {
        public Polygon(List<Vector3D> vertices, Plane plane)
        {
            Vertices = vertices;
            Plane = plane;
        }

        public List<Vector3D> Vertices { get; private set; }
        public Plane Plane { get; private set; }

        public void Flip()
        {
            Vertices.Reverse();
            Plane = Plane.Flipped();
        }
    }

    private sealed class Node
    {
        private Plane? _plane;
        private Node? _front;
        private Node? _back;
        private readonly List<Polygon> _polygons = new();

        public Node(List<Polygon> polygons) => Build(polygons);

        public void Invert()
        {
            foreach (Polygon p in _polygons) p.Flip();
            if (_plane is { } pl) _plane = pl.Flipped();
            _front?.Invert();
            _back?.Invert();
            (_front, _back) = (_back, _front);
        }

        private List<Polygon> ClipPolygons(List<Polygon> polygons)
        {
            if (_plane is not { } plane)
                return new List<Polygon>(polygons);

            var front = new List<Polygon>();
            var back = new List<Polygon>();
            foreach (Polygon p in polygons)
                plane.Split(p, front, back, front, back);

            if (_front is { } f) front = f.ClipPolygons(front);
            back = _back is { } b ? b.ClipPolygons(back) : new List<Polygon>();

            front.AddRange(back);
            return front;
        }

        public void ClipTo(Node other)
        {
            var clipped = other.ClipPolygons(_polygons);
            _polygons.Clear();
            _polygons.AddRange(clipped);
            _front?.ClipTo(other);
            _back?.ClipTo(other);
        }

        public List<Polygon> AllPolygons()
        {
            var result = new List<Polygon>(_polygons);
            if (_front is { } f) result.AddRange(f.AllPolygons());
            if (_back is { } b) result.AddRange(b.AllPolygons());
            return result;
        }

        public void Build(List<Polygon> polygons)
        {
            if (polygons.Count == 0)
                return;

            _plane ??= polygons[0].Plane;
            var front = new List<Polygon>();
            var back = new List<Polygon>();
            foreach (Polygon p in polygons)
                _plane.Value.Split(p, _polygons, _polygons, front, back);

            if (front.Count > 0)
                (_front ??= new Node(new List<Polygon>())).Build(front);
            if (back.Count > 0)
                (_back ??= new Node(new List<Polygon>())).Build(back);
        }
    }
}
